namespace Hunting.Web.Dashboards.Persistence;

public sealed class DashboardRepositoryException : Exception
{
    public DashboardRepositoryException(string message)
        : base(message)
    {
    }

    public DashboardRepositoryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public DashboardRepositoryException() : base()
    {
    }
}