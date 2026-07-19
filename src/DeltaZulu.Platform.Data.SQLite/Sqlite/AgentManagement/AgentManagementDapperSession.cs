using DeltaZulu.Platform.Domain.AgentManagement.Contracts;

namespace DeltaZulu.Platform.Data.Sqlite.AgentManagement;

public sealed class AgentManagementDapperSession(string connectionString)
    : DapperSessionBase(connectionString), IAgentManagementUnitOfWork;
