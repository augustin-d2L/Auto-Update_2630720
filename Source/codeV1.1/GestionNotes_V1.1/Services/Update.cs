using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GestionNotes.Services
{
    /// <summary>
    /// Contenu du fichier version.json du dossier réseau.
    /// </summary>
    internal class Manifeste
    {
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("fichier")]
        public string? Fichier { get; set; }
    }

    /// <summary>
    /// Informations nécessaires pour appliquer la mise à jour (étape suivante).
    /// </summary>
    internal class ResultatTelechargement
    {
        public bool Succes { get; set; }
        public string? MessageErreur { get; set; }

        public string? CheminArchiveLocale { get; set; }
        public Version? VersionCible { get; set; }
        public string? DossierInstallation { get; set; }
        public string? CheminExecutable { get; set; }
    }

    /// <summary>
    /// Résultat de la vérification, utilisé par l'interface pour décider quoi afficher.
    /// </summary>
    internal class ResultatVerification
    {
        public bool MiseAJourDisponible { get; set; }
        public Version? VersionInstallee { get; set; }
        public Version? VersionDisponible { get; set; }
        public string? Fichier { get; set; }
        public string? MessageErreur { get; set; }   // null = aucune erreur
    }

    internal class ServiceAutoUpdate
    {
        private readonly string _chemin;

        private readonly JsonSerializerOptions _options = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ServiceAutoUpdate()
        {
            _chemin = @"\\10.77.64.38\GestionNotes-Depot";
        }

        /// <summary>
        /// Vérifie si une nouvelle version est disponible.
        /// Ne lance jamais d'exception : en cas de problème, MessageErreur est rempli
        /// et l'application peut continuer normalement.
        /// </summary>
        public async Task<ResultatVerification> VerifierMiseAJourAsync()
        {
            var resultat = new ResultatVerification
            {
                VersionInstallee = ObtenirVersionInstallee()
            };

            try
            {
                // 1. Accéder au fichier version.json dans le dossier réseau
                string cheminManifeste = Path.Combine(_chemin, "version.json");

                // Task.Run : un partage réseau inaccessible peut prendre plusieurs
                // secondes à répondre, on évite de figer l'interface.
                string? contenu = await Task.Run(() =>
                    File.Exists(cheminManifeste) ? File.ReadAllText(cheminManifeste) : null);

                if (contenu == null)
                {
                    resultat.MessageErreur =
                        "Le fichier de version est introuvable ou le dossier réseau est inaccessible.";
                    return resultat;
                }

                // 2. Lire et désérialiser le contenu du fichier
                Manifeste? manifeste = JsonSerializer.Deserialize<Manifeste>(contenu, _options);

                if (manifeste == null
                    || string.IsNullOrWhiteSpace(manifeste.Version)
                    || string.IsNullOrWhiteSpace(manifeste.Fichier))
                {
                    resultat.MessageErreur = "Le manifeste de mise à jour est incomplet.";
                    return resultat;
                }

                // 3. Récupérer le numéro de la version disponible (System.Version)
                if (!Version.TryParse(manifeste.Version, out Version? versionDisponible))
                {
                    resultat.MessageErreur =
                        $"Le numéro de version « {manifeste.Version} » du manifeste est invalide.";
                    return resultat;
                }

                resultat.VersionDisponible = Normaliser(versionDisponible);
                resultat.Fichier = manifeste.Fichier;

                // 4 et 5. Comparer avec la version installée et déterminer
                // si une mise à jour est nécessaire
                resultat.MiseAJourDisponible =
                    resultat.VersionDisponible > resultat.VersionInstallee;
            }
            catch (JsonException)
            {
                resultat.MessageErreur = "Le manifeste de mise à jour est invalide (JSON mal formé).";
            }
            catch (UnauthorizedAccessException)
            {
                resultat.MessageErreur = "Accès refusé au dossier de mise à jour.";
            }
            catch (IOException ex)
            {
                resultat.MessageErreur = $"Le dossier de mise à jour est inaccessible : {ex.Message}";
            }

            return resultat;
        }

        /// <summary>
        /// Copie l'archive ZIP du dépôt réseau vers un dossier temporaire local
        /// et vérifie que la copie est complète et valide.
        /// Ne lance jamais d'exception : en cas de problème, MessageErreur est rempli.
        /// </summary>
        public async Task<ResultatTelechargement> TelechargerMiseAJourAsync(ResultatVerification verification)
        {
            var resultat = new ResultatTelechargement();

            try
            {
                // Sécurité : on ne garde que le nom du fichier (empêche un "..\..\" dans le manifeste)
                string nomFichier = Path.GetFileName(verification.Fichier ?? "");
                if (string.IsNullOrWhiteSpace(nomFichier))
                {
                    resultat.MessageErreur = "Le nom de l'archive de mise à jour est invalide.";
                    return resultat;
                }

                string cheminSource = Path.Combine(_chemin, nomFichier);

                // 1. Vérifier que l'archive ZIP existe dans le dépôt réseau
                bool existe = await Task.Run(() => File.Exists(cheminSource));
                if (!existe)
                {
                    resultat.MessageErreur = $"L'archive « {nomFichier} » est introuvable dans le dépôt réseau.";
                    return resultat;
                }

                // 2. Copier l'archive dans un répertoire temporaire local
                string dossierTemp = Path.Combine(Path.GetTempPath(), "GestionNotes-Update");
                Directory.CreateDirectory(dossierTemp);

                string cheminLocal = Path.Combine(dossierTemp, nomFichier);

                await Task.Run(() => File.Copy(cheminSource, cheminLocal, overwrite: true));

                // 3. Vérifier que la copie s'est terminée correctement
                //    a) même taille que l'original
                long tailleSource = new FileInfo(cheminSource).Length;
                long tailleLocale = new FileInfo(cheminLocal).Length;

                if (tailleSource != tailleLocale)
                {
                    File.Delete(cheminLocal);
                    resultat.MessageErreur = "La copie de l'archive est incomplète.";
                    return resultat;
                }

                //    b) l'archive locale est un ZIP lisible et non vide
                await Task.Run(() =>
                {
                    using ZipArchive zip = ZipFile.OpenRead(cheminLocal);
                    if (zip.Entries.Count == 0)
                        throw new InvalidDataException("L'archive est vide.");
                });

                // 4. Préparer les informations nécessaires pour appliquer la mise à jour
                resultat.CheminArchiveLocale = cheminLocal;
                resultat.VersionCible = verification.VersionDisponible;
                resultat.DossierInstallation = AppContext.BaseDirectory;
                resultat.CheminExecutable = Environment.ProcessPath;
                resultat.Succes = true;
            }
            catch (InvalidDataException)
            {
                resultat.MessageErreur = "L'archive de mise à jour est corrompue ou n'est pas un fichier ZIP valide.";
            }
            catch (UnauthorizedAccessException)
            {
                resultat.MessageErreur = "Accès refusé lors de la copie de l'archive.";
            }
            catch (IOException ex)
            {
                resultat.MessageErreur = $"La copie de l'archive a échoué : {ex.Message}";
            }

            return resultat;
        }

        /// <summary>
        /// Version de l'application en cours d'exécution (balise &lt;Version&gt; du .csproj).
        /// </summary>
        private static Version ObtenirVersionInstallee()
        {
            Version? version = Assembly.GetEntryAssembly()?.GetName().Version;
            return Normaliser(version ?? new Version(0, 0));
        }

        /// <summary>
        /// Met toutes les versions au format x.y.z.w pour que la comparaison soit juste :
        /// sans ça, "1.2" (Build = -1) serait considérée plus petite que "1.2.0.0".
        /// </summary>
        private static Version Normaliser(Version v)
        {
            return new Version(
                v.Major,
                v.Minor,
                Math.Max(v.Build, 0),
                Math.Max(v.Revision, 0));
        }
    }
}