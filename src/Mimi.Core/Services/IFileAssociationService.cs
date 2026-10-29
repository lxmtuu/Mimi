namespace Mimi.Services;

/// <summary>
/// Registers Mimi as an optional handler in the operating system's
/// "Open with" experience for the current user. Registration must not claim
/// or overwrite the user's protected default-app choice.
/// </summary>
public interface IFileAssociationService
{
    bool IsRegistered { get; }

    string ExecutablePath { get; }

    void Register();

    void Unregister();
}
