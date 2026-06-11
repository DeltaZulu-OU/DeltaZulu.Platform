namespace DeltaZulu.Platform.Data.Sqlite.Hunting;

using System.Data.Common;

public interface IAppDbConnectionFactory
{
    DbConnection CreateConnection();
}