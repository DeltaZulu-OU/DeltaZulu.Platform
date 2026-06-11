namespace DeltaZulu.Platform.Data.Hunting.Persistence;

using System.Data.Common;

public interface IAppDbConnectionFactory
{
    DbConnection CreateConnection();
}