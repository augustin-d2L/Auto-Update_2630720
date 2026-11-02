
using System.Windows.Input;

namespace GestionNotes.ViewModels;

public class Commande : ICommand
{
    private readonly Action _exécuter;
    private readonly Func<bool>? _peutExécuter;

    public Commande(Action exécuter, Func<bool>? peutExécuter = null)
    {
        _exécuter = exécuter;
        _peutExécuter = peutExécuter;
    }

    public bool CanExecute(object? paramètre)
    {
        return _peutExécuter?.Invoke() ?? true;
    }

    public void Execute(object? paramètre)
    {
        _exécuter();
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
