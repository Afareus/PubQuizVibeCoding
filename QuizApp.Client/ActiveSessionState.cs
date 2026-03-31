namespace QuizApp.Client;

public sealed class ActiveSessionState
{
    public bool IsActive { get; private set; }
    public string ConfirmMessage { get; private set; } = string.Empty;
    public Func<Task>? LeaveAction { get; private set; }

    public void Set(string confirmMessage, Func<Task> leaveAction)
    {
        IsActive = true;
        ConfirmMessage = confirmMessage;
        LeaveAction = leaveAction;
    }

    public void Clear()
    {
        IsActive = false;
        ConfirmMessage = string.Empty;
        LeaveAction = null;
    }
}
