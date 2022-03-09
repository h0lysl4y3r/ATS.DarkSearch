using ServiceStack;
using ServiceStack.Messaging;

namespace ATS.DarkSearch.Helpers;

public static class AppHostHelpers
{
    public static T GetAppHost<T>()
        where T : AppHostBase
    {
        return HostContext.AppHost as T;
    }
    
    public static T GetMessageService<T>()
        where T : class, IMessageService
    {
        if (HostContext.AppHost == null)
            return null;

        return HostContext.TryResolve<T>();
    }
}