using MassTransit;

namespace SagaTest;

public class MySagaDefinition : SagaDefinition<MySagaState>
{
    protected override void ConfigureSaga(IReceiveEndpointConfigurator endpointConfigurator,
        ISagaConfigurator<MySagaState> sagaConfigurator,
        IRegistrationContext context)
    {
        sagaConfigurator.UseMessageRetry(x => x.Interval(50, TimeSpan.FromSeconds(5)));
    }
}
