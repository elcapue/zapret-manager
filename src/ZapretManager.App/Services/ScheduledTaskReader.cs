using System.Reflection;
using System.Runtime.InteropServices;

namespace ZapretManager.App.Services;

public enum ScheduledTaskLookupStatus
{
    Exists,
    Missing,
    Failed
}

public sealed record ScheduledTaskLookup(ScheduledTaskLookupStatus Status, string Xml = "", string Error = "");

public interface IScheduledTaskReader
{
    ScheduledTaskLookup Read(string taskName);
}

/// <summary>
/// Читает задание из корневой папки Планировщика через его COM-API.
/// Отсутствие задания определяется по коду ошибки, а не по тексту schtasks,
/// который зависит от языка Windows.
/// </summary>
public sealed class ScheduledTaskReader : IScheduledTaskReader
{
    private const int FileNotFound = unchecked((int)0x80070002);
    private const int PathNotFound = unchecked((int)0x80070003);

    public ScheduledTaskLookup Read(string taskName)
    {
        object? service = null;
        try
        {
            var serviceType = Type.GetTypeFromProgID("Schedule.Service", throwOnError: true)!;
            service = Activator.CreateInstance(serviceType)!;
            dynamic scheduler = service;
            scheduler.Connect();
            dynamic rootFolder = scheduler.GetFolder("\\");
            dynamic task = rootFolder.GetTask(taskName);
            string xml = task.Xml;
            return new ScheduledTaskLookup(ScheduledTaskLookupStatus.Exists, xml);
        }
        catch (Exception ex)
        {
            var error = (ex as TargetInvocationException)?.InnerException ?? ex;
            return error.HResult is FileNotFound or PathNotFound
                ? new ScheduledTaskLookup(ScheduledTaskLookupStatus.Missing)
                : new ScheduledTaskLookup(ScheduledTaskLookupStatus.Failed, Error: error.Message);
        }
        finally
        {
            if (service is not null && Marshal.IsComObject(service))
            {
                Marshal.FinalReleaseComObject(service);
            }
        }
    }
}
