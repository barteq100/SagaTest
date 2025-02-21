using MassTransit;
using SagaTest.Events;

namespace SagaTest;

public class MySagaStateMachine : MassTransitStateMachine<MySagaState>
{
    public State WaitingForCompletion { get; set; } = null!;
    public State RunningJobC { get; set; } = null!;
    public Event<StartSaga> ExecuteEvent { get; set; } = null!;
    public Event JobAAndBDone { get; set; } = null!;
    public Event JobBCompleted { get; set; } = null!;
    public Event JobACompleted { get; set; } = null!;
    public Request<MySagaState, StartJobA, JobADone> JobA { get; set; } = null!;
    public Request<MySagaState, StartJobB, JobBDone> JobB { get; set; } = null!;
    public Request<MySagaState, StartJobC, JobCDone> JobC { get; set; } = null!;

    public MySagaStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => ExecuteEvent,
            x => x.CorrelateById(m => m.Message.JobId));

        Event(() => JobBCompleted);
        Event(() => JobACompleted);
        Event(() => JobAAndBDone);


        ConfigureRequests();

        Initially(
            When(ExecuteEvent)
                .Request(JobA, async c => new StartJobA(c.Message.JobId))
                .Request(JobB, async c => new StartJobB(c.Message.JobId))
                .TransitionTo(WaitingForCompletion)
        );

        During(WaitingForCompletion,
            When(JobB.Completed)
                .ThenAsync(async x =>
                {
                    x.Saga.JobBDone = true;
                })
                .TransitionTo(WaitingForCompletion)
                .ThenAsync(async x => await x.Raise(JobBCompleted))
        );

        During(WaitingForCompletion,
            When(JobA.Completed)
                .ThenAsync(async x =>
                {
                    x.Saga.JobADone = true;
                })
                .TransitionTo(WaitingForCompletion)
                .ThenAsync(async x => await x.Raise(JobACompleted))
        );

        CompositeEvent(() => JobAAndBDone,
            x => x.DataPreparedStatus,
            JobBCompleted,
            JobACompleted);

        During(WaitingForCompletion,
            When(JobAAndBDone)
                .Request(JobC,
                    x => new StartJobC(x.Saga.CorrelationId))
                .TransitionTo(RunningJobC)
        );

        During(RunningJobC,
            When(JobC.Completed)
                .Then(x => x.Saga.ResultingEntityId = x.Message.EntityId)
                .Finalize()
        );

        Finally(x =>
        {
            //Cleanup
            return x;
        });

        SetCompletedWhenFinalized();
    }

    private void ConfigureRequests()
    {
        Request(() => JobA,
            r =>
            {
                r.Completed = e => e.ConfigureConsumeTopology = false;
                r.Faulted = e => e.ConfigureConsumeTopology = false;
                r.TimeoutExpired = e => e.ConfigureConsumeTopology = false;
            });

        Request(() => JobB,
            r =>
            {
                r.Completed = e => e.ConfigureConsumeTopology = false;
                r.Faulted = e => e.ConfigureConsumeTopology = false;
                r.TimeoutExpired = e => e.ConfigureConsumeTopology = false;
            });

        Request(() => JobC,
            r =>
            {
                r.Completed = e => e.ConfigureConsumeTopology = false;
                r.Faulted = e => e.ConfigureConsumeTopology = false;
                r.TimeoutExpired = e => e.ConfigureConsumeTopology = false;
            });
    }
}
