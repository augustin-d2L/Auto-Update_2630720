
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using GestionNotes.Models;
using GestionNotes.Services;
using System.Linq;
using System.Windows;

namespace GestionNotes.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ServiceSauvegarde _serviceSauvegarde = new();

    private bool _sauvegardeAutorisée = true;
    public ObservableCollection<Étudiant> Étudiants { get; set; }

    private string _prénom = string.Empty;
    public string Prénom
    {
        get => _prénom;
        set
        {
            if (_prénom != value)
            {
                _prénom = value;
                OnPropertyChanged();
            }
        }
    }

    private string _nom = string.Empty;
    public string Nom
    {
        get => _nom;
        set
        {
            if (_nom != value)
            {
                _nom = value;
                OnPropertyChanged();
            }
        }
    }

    private Étudiant? _étudiantSélectionné;
    public Étudiant? ÉtudiantSélectionné
    {
        get => _étudiantSélectionné;
        set
        {
            if (_étudiantSélectionné != value)
            {
                _étudiantSélectionné = value;
                OnPropertyChanged();
            }
        }
    }

    private string _noteÀAjouter = string.Empty;
    public string NoteÀAjouter
    {
        get => _noteÀAjouter;
        set
        {
            if (_noteÀAjouter != value)
            {
                _noteÀAjouter = value;
                OnPropertyChanged();
            }
        }
    }

    public ICommand AjouterÉtudiantCommande { get; }
    public ICommand AjouterNoteCommande { get; }

    public int NombreÉtudiants => Étudiants.Count;

    public int NombreNotes =>
        Étudiants.Sum(étudiant => étudiant.Notes.Count);

    public double MoyenneGroupe
    {
        get
        {
            List<double> notes = Étudiants
                .SelectMany(étudiant => étudiant.Notes)
                .ToList();

            return notes.Count == 0 ? 0 : notes.Average();
        }
    }


    public MainViewModel()
    {
        try
        {
            Étudiants = _serviceSauvegarde.Charger();
        }
        catch (Exception ex)
        {
            Étudiants = new ObservableCollection<Étudiant>();
            _sauvegardeAutorisée = false;

            MessageBox.Show(
                $"Impossible de charger les données.\n\n{ex.Message}\n\n" +
                "Les modifications sont désactivées afin de protéger " +
                "la sauvegarde existante.",
                "Erreur de chargement",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        AjouterÉtudiantCommande = new Commande(AjouterÉtudiant);
        AjouterNoteCommande = new Commande(AjouterNote);
    }

    private void AjouterÉtudiant()
    {
        if (!_sauvegardeAutorisée)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(Prénom) ||
            string.IsNullOrWhiteSpace(Nom))
        {
            return;
        }

        Étudiants.Add(new Étudiant
        {
            Id = Étudiants.Count + 1,
            Prénom = Prénom.Trim(),
            Nom = Nom.Trim()
        });
        ActualiserStatistiques();
        Sauvegarder();

        Prénom = string.Empty;
        Nom = string.Empty;
    }

    private void AjouterNote()
    {
        if (!_sauvegardeAutorisée)
        {
            return;
        }
        if (ÉtudiantSélectionné == null)
        {
            return;
        }

        if (!double.TryParse(NoteÀAjouter, out double note))
        {
            return;
        }

        if (note < 0 || note > 100)
        {
            return;
        }

        ÉtudiantSélectionné.Notes.Add(note);
        ActualiserStatistiques();
        Sauvegarder();
        NoteÀAjouter = string.Empty;
    }
    private void Sauvegarder()
    {
        try
        {
            _serviceSauvegarde.Sauvegarder(Étudiants);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Impossible de sauvegarder les données.\n\n{ex.Message}",
                "Erreur de sauvegarde",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ActualiserStatistiques()
    {
        OnPropertyChanged(nameof(NombreÉtudiants));
        OnPropertyChanged(nameof(NombreNotes));
        OnPropertyChanged(nameof(MoyenneGroupe));
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
