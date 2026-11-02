
using GestionNotes.Services;
using GestionNotes.ViewModels;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace GestionNotes.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContext = new MainViewModel();
    }
    private ResultatTelechargement? _miseAJourPrete;   // gardé pour l'étape d'application

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var service = new ServiceAutoUpdate();
        ResultatVerification verification = await service.VerifierMiseAJourAsync();

        // Partage inaccessible ou manifeste invalide : on prévient et on continue
        if (verification.MessageErreur != null)
        {
            MessageBox.Show(verification.MessageErreur, "Vérification des mises à jour",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Aucune nouvelle version : on continue normalement
        if (!verification.MiseAJourDisponible)
            return;

        // Informer l'utilisateur et lui permettre d'accepter ou de refuser
        MessageBoxResult choix = MessageBox.Show(
            $"Une nouvelle version de GestionNotes est disponible.\n\n" +
            $"Version installée : {verification.VersionInstallee}\n" +
            $"Nouvelle version : {verification.VersionDisponible}\n\n" +
            $"Voulez-vous télécharger la mise à jour ?",
            "Mise à jour disponible", MessageBoxButton.YesNo, MessageBoxImage.Question);

        // Refus : l'application continue normalement
        if (choix != MessageBoxResult.Yes)
            return;

        ResultatTelechargement telechargement = await service.TelechargerMiseAJourAsync(verification);

        if (!telechargement.Succes)
        {
            // ZIP absent ou copie échouée : message d'erreur, on NE ferme PAS l'application
            MessageBox.Show(telechargement.MessageErreur, "Échec du téléchargement",
                            MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        
        LancerMiseAJour(telechargement);
    }

    private void LancerMiseAJour(ResultatTelechargement maj)
    {
        try
        {
            // Updater ne peut pas se remplacer lui-même pendant qu'il tourne :
            // on le copie dans un dossier temporaire et on le lance depuis là.
            string dossierUpdater = Path.Combine(Path.GetTempPath(), "GestionNotes-Update", "updater");
            Directory.CreateDirectory(dossierUpdater);

            foreach (string fichier in Directory.GetFiles(AppContext.BaseDirectory, "Updater.*"))
            {
                File.Copy(fichier, Path.Combine(dossierUpdater, Path.GetFileName(fichier)), overwrite: true);
            }

            string updaterExe = Path.Combine(dossierUpdater, "Updater.exe");
            if (!File.Exists(updaterExe))
            {
                MessageBox.Show("Le programme Updater.exe est introuvable. La mise à jour est annulée.",
                                "Mise à jour", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // TrimEnd : un "\" final devant un guillemet casserait les arguments
            string dossierInstallation = maj.DossierInstallation!.TrimEnd('\\');

            var info = new ProcessStartInfo(updaterExe)
            {
                Arguments = $"\"{maj.CheminArchiveLocale}\" \"{dossierInstallation}\" " +
                            $"{Environment.ProcessId} \"{maj.CheminExecutable}\"",
                UseShellExecute = true
            };

            // Installé dans Program Files : il faut les droits administrateur pour remplacer les fichiers
            if (!PeutEcrireDans(dossierInstallation))
            {
                info.Verb = "runas";
            }

            Process.Start(info);

            // Le programme principal se ferme proprement ; Updater attend sa fin complète
            Application.Current.Shutdown();
        }
        catch (Win32Exception)
        {
            // L'utilisateur a refusé la demande d'élévation (UAC)
            MessageBox.Show("La mise à jour a été annulée : les droits administrateur ont été refusés.",
                            "Mise à jour", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible de lancer la mise à jour : {ex.Message}",
                            "Mise à jour", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static bool PeutEcrireDans(string dossier)
    {
        try
        {
            string test = Path.Combine(dossier, $".test_{Guid.NewGuid():N}");
            File.WriteAllText(test, "");
            File.Delete(test);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
