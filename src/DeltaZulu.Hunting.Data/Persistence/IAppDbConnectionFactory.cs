namespace DeltaZulu.Hunting.Data.Persistence;

using System.Data.Common;

public interface IAppDbConnectionFactory
{
    DbConnection CreateConnection();
}