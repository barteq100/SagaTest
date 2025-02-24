using JasperFx.Core;
using Marten;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using SagaTest.Events;
using Testcontainers.PostgreSql;

namespace SagaTest.Tests;

public class SagaTests : IAsyncLifetime
{
    
    private readonly Guid resultingEntity = Guid.NewGuid();
    private readonly Guid jobId = Guid.NewGuid();
    
    private readonly PostgreSqlContainer postgres;
    private ITestHarness Harness { get; } = default!;
    private ServiceProvider Provider { get; } = default!;

    public IDocumentStore Store { get; set; }

    public IServiceScope Scope { get; set; }

    public SagaTests()
    {
        postgres = new PostgreSqlBuilder()
            .WithImage("postgres:14-alpine")
            .Build();

        postgres.StartAsync().GetAwaiter().GetResult();
        
        Provider = new ServiceCollection()
            .AddMarten(cfg =>
            {
                cfg.Connection(postgres.GetConnectionString());
            })
            .ApplyAllDatabaseChangesOnStartup()
            .UseDirtyTrackedSessions()
            .Services
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddSagaStateMachine<MySagaStateMachine, MySagaState>(typeof(MySagaDefinition))
                    //.InMemoryRepository();
                    .MartenRepository(r =>
                    {
                        r.Index(x => x.CorrelationId);
                        r.UseOptimisticConcurrency(true);
                    });
                
                cfg.AddConfigureEndpointsCallback((context,name,cfg) =>
                {
                    cfg.UseMessageRetry(r => r.Immediate(5));
                    cfg.UseInMemoryOutbox(context);
                });

                cfg.AddHandler<StartJobA>(async cxt =>
                {
                    await Task.Delay(1.Seconds());
                    await cxt.RespondAsync(new JobADone(jobId));
                }); 
                
                cfg.AddHandler<StartJobB>(async cxt =>
                {
                    await Task.Delay(1.Seconds());
                    await cxt.RespondAsync(new JobBDone(jobId));
                });
                
                cfg.AddHandler<StartJobC>(async cxt =>
                {
                    await Task.Delay(2.Seconds());
                    await cxt.RespondAsync(new JobCDone(jobId, resultingEntity));
                });

  
            })
            .BuildServiceProvider(true);

        Harness = Provider.GetRequiredService<ITestHarness>();
        Harness.Start().GetAwaiter().GetResult();
    }

    public async Task InitializeAsync()
    { }

    public async Task DisposeAsync()
    {
        await Provider.DisposeAsync();
        await postgres.DisposeAsync();
    }
    
    [Fact]
    public async Task Should_Complete_Saga_When_All_Events_Are_Received()
    {
        await Harness.Bus.Publish(new StartSaga(jobId));

        var sagaHarness = Harness.GetSagaStateMachineHarness<MySagaStateMachine, MySagaState>();

        Assert.True(await sagaHarness.Consumed.Any<StartSaga>());
        Assert.True(await sagaHarness.Created.Any(x => x.CorrelationId == jobId));

        var sagaInWaitingForCompletion = sagaHarness.Created.ContainsInState(jobId, sagaHarness.StateMachine, sagaHarness.StateMachine.WaitingForCompletion);
        Assert.True(sagaInWaitingForCompletion is not null, "Saga not in waiting for completion");
        
        Assert.True(await sagaHarness.Consumed.Any<JobADone>());
        Assert.True(await sagaHarness.Consumed.Any<JobBDone>());
        //Assert.True(await Harness.Published.Any<StartJobC>());


        var sagaInRunningJobC = sagaHarness.Created.ContainsInState(jobId, sagaHarness.StateMachine, sagaHarness.StateMachine.RunningJobC);
        Assert.True(sagaInRunningJobC is not null, "Saga not in runing job C");

        Assert.True(await sagaHarness.Consumed.Any<JobCDone>());

        var finalInstance = sagaHarness.Created.ContainsInState(jobId, sagaHarness.StateMachine, sagaHarness.StateMachine.Final);
        Assert.NotNull(finalInstance);
        
        Assert.Equivalent(new MySagaState()
        {
            CorrelationId = jobId,
            CurrentState = "Final",
            DataPreparedStatus = new CompositeEventStatus(3),
            JobADone = true,
            JobBDone = true,
            ResultingEntityId = resultingEntity,
        }, finalInstance);
        

        var sagaRemoved = await sagaHarness.NotExists(jobId);
        Assert.Null(sagaRemoved);
    }
    
}
