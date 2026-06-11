
using System.Data.Common;

namespace DeltaZulu.Platform.Data.Sqlite.Hunting;
public interface IAppDbConnectionFactory
{
    DbConnection CreateConnection();
}