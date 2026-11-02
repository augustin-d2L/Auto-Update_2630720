
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using GestionNotes.Models;

namespace GestionNotes.Services;

public class ServiceSauvegarde
{
    private readonly string _cheminFichier;
    private readonly string _cheminTemporaire;

    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true
    };

    public ServiceSauvegarde()
    {
        string dossier = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "GestionNotes");

        Directory.CreateDirectory(dossier);

        _cheminFichier = Path.Combine(dossier, "etudiants.json");
        _cheminTemporaire = _cheminFichier + ".tmp";
    }

    public ObservableCollection<Étudiant> Charger()
    {
        if (!File.Exists(_cheminFichier))
        {
            return new ObservableCollection<Étudiant>();
        }

        string json = File.ReadAllText(_cheminFichier);

        List<Étudiant>? étudiants =
            JsonSerializer.Deserialize<List<Étudiant>>(json, _options);

        if (étudiants == null)
        {
            throw new InvalidDataException(
                "Le fichier de sauvegarde ne contient pas de données valides.");
        }

        ValiderDonnées(étudiants);

        return new ObservableCollection<Étudiant>(étudiants);
    }

    public void Sauvegarder(
        ObservableCollection<Étudiant> étudiants)
    {
        string json = JsonSerializer.Serialize(
            étudiants,
            _options);

        try
        {
            File.WriteAllText(_cheminTemporaire, json);

            if (File.Exists(_cheminFichier))
            {
                File.Replace(
                    _cheminTemporaire,
                    _cheminFichier,
                    null);
            }
            else
            {
                File.Move(_cheminTemporaire, _cheminFichier);
            }
        }
        finally
        {
            if (File.Exists(_cheminTemporaire))
            {
                File.Delete(_cheminTemporaire);
            }
        }
    }

    private static void ValiderDonnées(List<Étudiant> étudiants)
    {
        HashSet<int> identifiants = new();

        foreach (Étudiant étudiant in étudiants)
        {
            if (étudiant == null)
            {
                throw new InvalidDataException(
                    "Le fichier contient un étudiant invalide.");
            }

            if (étudiant.Id <= 0 ||
                !identifiants.Add(étudiant.Id))
            {
                throw new InvalidDataException(
                    "Un identifiant d'étudiant est invalide ou en double.");
            }

            if (string.IsNullOrWhiteSpace(étudiant.Prénom) ||
                string.IsNullOrWhiteSpace(étudiant.Nom))
            {
                throw new InvalidDataException(
                    "Un étudiant ne possède pas de prénom ou de nom.");
            }

            if (étudiant.Notes == null ||
                étudiant.Notes.Any(note =>
                    double.IsNaN(note) ||
                    double.IsInfinity(note) ||
                    note < 0 ||
                    note > 100))
            {
                throw new InvalidDataException(
                    $"Les notes de l'étudiant {étudiant.Id} sont invalides.");
            }
        }
    }
}
