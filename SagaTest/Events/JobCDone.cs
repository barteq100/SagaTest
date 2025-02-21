namespace SagaTest.Events;

public record JobCDone(Guid JobId, Guid EntityId);
