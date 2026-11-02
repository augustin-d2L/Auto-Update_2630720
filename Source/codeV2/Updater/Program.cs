using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace Updater
{
    internal class Program
    {
        private static readonly string DossierTravail =
            Path.Combine(Path.GetTempPath(), "GestionNotes-Update");

        private static readonly string FichierLog =
            Path.Combine(DossierTravail, "updater.log");

        /// <summary>
        /// Arguments attendus :
        ///   [0] chemin de l'archive ZIP locale
        ///   [1] dossier d'installation de GestionNotes
        ///   [2] PID du processus GestionNotes
        ///   [3] chemin de GestionNotes.exe à relancer
        /// </summary>
        static int Main(string[] args)
        {
            Directory.CreateDirectory(DossierTravail);
            Log("=== Démarrage de Updater ===");

            try
            {
                // 1. Recevoir les informations nécessaires
                if (args.Length < 4)
                {
                    Log("Arguments manquants. Utilisation : Updater.exe <zip> <dossier> <pid> <exe>");
                    return 1;
                }

                string cheminZip = args[0];
                string dossierInstallation = args[1];
                int pid = int.Parse(args[2]);
                string cheminExe = args[3];

                Log($"Archive : {cheminZip}");
                Log($"Dossier d'installation : {dossierInstallation}");
                Log($"PID de GestionNotes : {pid}");

                if (!File.Exists(cheminZip))
                    throw new FileNotFoundException("Archive locale introuvable.", cheminZip);

                if (!Directory.Exists(dossierInstallation))
                    throw new DirectoryNotFoundException($"Dossier d'installation introuvable : {dossierInstallation}");

                // 2. Attendre que GestionNotes soit complètement terminé
                AttendreFinProcessus(pid);

                // 3. Extraire l'archive ZIP dans un répertoire temporaire
                string dossierExtraction = Path.Combine(DossierTravail, "extraction");
                if (Directory.Exists(dossierExtraction))
                    Directory.Delete(dossierExtraction, recursive: true);

                Log("Extraction de l'archive...");
                ZipFile.ExtractToDirectory(cheminZip, dossierExtraction);

                // Si le ZIP contient un seul dossier racine (ex. "GestionNotes-1.1\"), on entre dedans
                string source = TrouverDossierSource(dossierExtraction);

                // 4. Remplacer les fichiers de l'ancienne version
                Log("Remplacement des fichiers...");
                CopierDossier(source, dossierInstallation);

                // 5. Démarrer la nouvelle version de GestionNotes
                Log("Redémarrage de GestionNotes...");
                Process.Start(new ProcessStartInfo(cheminExe)
                {
                    UseShellExecute = true,
                    WorkingDirectory = dossierInstallation
                });

                // 6. Terminer l'exécution
                Log("Mise à jour terminée avec succès.");
                return 0;
            }
            catch (Exception ex)
            {
                Log($"ERREUR : {ex}");
                Console.WriteLine();
                Console.WriteLine("La mise à jour a échoué. Détails dans :");
                Console.WriteLine(FichierLog);
                Console.WriteLine("Appuyez sur une touche pour fermer...");
                Console.ReadKey();
                return 1;
            }
        }

        /// <summary>
        /// Attend la fin complète du processus GestionNotes avant de toucher aux fichiers.
        /// </summary>
        private static void AttendreFinProcessus(int pid)
        {
            Log("Attente de la fermeture de GestionNotes...");
            try
            {
                using Process processus = Process.GetProcessById(pid);

                if (!processus.WaitForExit(30_000))   // 30 secondes maximum
                    throw new TimeoutException("GestionNotes ne s'est pas fermé dans les 30 secondes.");
            }
            catch (ArgumentException)
            {
                // GetProcessById lance ArgumentException si le processus est déjà terminé
            }

            Log("GestionNotes est fermé.");
        }

        private static string TrouverDossierSource(string dossierExtraction)
        {
            string[] fichiers = Directory.GetFiles(dossierExtraction);
            string[] dossiers = Directory.GetDirectories(dossierExtraction);

            return (fichiers.Length == 0 && dossiers.Length == 1) ? dossiers[0] : dossierExtraction;
        }

        /// <summary>
        /// Copie récursivement tous les fichiers de source vers destination (en écrasant).
        /// </summary>
        private static void CopierDossier(string source, string destination)
        {
            foreach (string fichier in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relatif = Path.GetRelativePath(source, fichier);
                string cible = Path.Combine(destination, relatif);

                Directory.CreateDirectory(Path.GetDirectoryName(cible)!);
                CopierAvecReessais(fichier, cible);
                Log($"  Remplacé : {relatif}");
            }
        }

        /// <summary>
        /// Windows peut garder un fichier verrouillé quelques instants après la fin d'un processus :
        /// on réessaie plusieurs fois avant d'abandonner.
        /// </summary>
        private static void CopierAvecReessais(string source, string cible)
        {
            const int essaisMax = 5;

            for (int essai = 1; ; essai++)
            {
                try
                {
                    File.Copy(source, cible, overwrite: true);
                    return;
                }
                catch (IOException) when (essai < essaisMax)
                {
                    Thread.Sleep(500);
                }
            }
        }

        private static void Log(string message)
        {
            string ligne = $"[{DateTime.Now:HH:mm:ss}] {message}";
            Console.WriteLine(ligne);
            File.AppendAllText(FichierLog, ligne + Environment.NewLine);
        }
    }
}