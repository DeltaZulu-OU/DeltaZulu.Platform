
using System.Data.Common;

namespace DeltaZulu.Platform.Data.Sqlite.Analytics;
public interface IAppDbConnectionFactory
{
    DbConnection CreateConnection();
}