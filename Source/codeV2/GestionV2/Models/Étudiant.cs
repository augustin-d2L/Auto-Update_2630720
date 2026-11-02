
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GestionNotes.Models;

public class Étudiant : INotifyPropertyChanged
{
    public int Id { get; set; }

    public string Prénom { get; set; } = string.Empty;

    public string Nom { get; set; } = string.Empty;


    private ObservableCollection<double> _notes = new();

    public ObservableCollection<double> Notes
    {
        get => _notes;
        set
        {
            if (_notes != value)
            {
                _notes.CollectionChanged -= Notes_CollectionChanged;

                _notes = value;

                _notes.CollectionChanged += Notes_CollectionChanged;

                OnPropertyChanged();
                OnPropertyChanged(nameof(Moyenne));
            }
        }
    }


    public double Moyenne => CalculerMoyenne();

    public Étudiant()
    {
        _notes.CollectionChanged += Notes_CollectionChanged;
    }

    private void Notes_CollectionChanged(
        object? expéditeur,
        NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Moyenne));
    }

    public double CalculerMoyenne()
    {
        if (Notes.Count == 0)
        {
            return 0;
        }

        return Notes.Average();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(
        [CallerMemberName] string? nomPropriété = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(nomPropriété));
    }
}
