namespace ZapretManager.App.Core;

public enum ZapretState
{
    RuntimeMissing,
    Stopped,
    Running,
    External
}

public static class ZapretStateText
{
    public static string ToDisplayText(this ZapretState state)
    {
        return state switch
        {
            ZapretState.Running => "Включено",
            ZapretState.Stopped => "Выключено",
            ZapretState.External => "Внешний zapret",
            _ => "Нет runtime"
        };
    }
}
