using MassTransit;

namespace SagaTest;

public class MySagaState : SagaStateMachineInstance
{
    public string CurrentState { get; set; }

    public bool JobADone { get; set; } = false;
    public bool JobBDone { get; set; } = false;
    public Guid ResultingEntityId { get; set; }

    public CompositeEventStatus DataPreparedStatus { get; set; }

    public Guid CorrelationId { get; set; }
}
