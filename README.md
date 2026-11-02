# GestionNotes — Mise à jour automatique

Application WPF **GestionNotes** et programme auxiliaire **Updater.exe** : l'application détecte une
nouvelle version dans un dossier réseau partagé, la télécharge, puis Updater remplace les fichiers et
redémarre l'application.

## 1. Chemin UNC

```
\\10.77.64.38\GestionNotes-Depot
```

Défini dans `GestionNotes/Services/ServiceAutoUpdate.cs`.

Le dossier `C:\GestionNotes-Depot` de la VM est partagé sous le nom `GestionNotes-Depot` :
- **Partage** : Utilisateurs authentifiés en lecture ;
- **NTFS** (onglet Sécurité) : Utilisateurs authentifiés en lecture et exécution.

> **Capture : partage réseau accessible depuis le poste de travail**
>
> <img width="1132" height="643" alt="Capture d’écran 2026-11-02 141422" src="https://github.com/user-attachments/assets/be1e1bf6-6758-4a8a-b72c-e8767bafefae" />


## 2. Publier une nouvelle version

1. Dans `GestionNotes.csproj`, changer le numéro : `<Version>1.1</Version>`.
2. Compiler en **Release**. Les fichiers `Updater.*` doivent être à côté de `GestionNotes.exe`.
3. Sélectionner **tous les fichiers** du dossier de sortie, puis les compresser en `GestionNotes-1.1.zip`.
4. Copier le ZIP dans le dossier partagé.
5. Mettre à jour `version.json` dans le dossier partagé :

```json
{
  "version": "1.1",
  "fichier": "GestionNotes-1.1.zip"
}
```

Aucune modification de l'application installée n'est nécessaire.

## 3. Fonctionnement

1. Au démarrage, GestionNotes lit `version.json` et compare la version avec `System.Version`
   (1.10 est donc plus récente que 1.2).
2. Si une version plus récente existe, l'utilisateur choisit de l'installer ou non.
   S'il refuse, l'application continue normalement.
3. S'il accepte, le ZIP est copié dans `%TEMP%\GestionNotes-Update\`, puis la copie est vérifiée
   (taille identique, ZIP lisible).
4. GestionNotes lance `Updater.exe` en lui transmettant le chemin du ZIP, le dossier d'installation
   et son PID, puis se ferme.
5. Updater attend la fermeture complète de GestionNotes, extrait le ZIP, remplace les fichiers,
   relance GestionNotes et se termine.

**Pourquoi un programme auxiliaire ?** Une application ne peut pas remplacer ses propres fichiers
pendant qu'elle s'exécute.

**Pourquoi une copie locale ?** Pour que le remplacement des fichiers ne dépende pas du réseau.

En cas d'erreur (partage inaccessible, manifeste invalide, ZIP absent ou copie échouée), un message
s'affiche et l'application reste ouverte et utilisable. Le journal d'Updater se trouve dans
`%TEMP%\GestionNotes-Update\updater.log`.

## 4. Validation

| Scénario | Résultat attendu |
|---|---|
| 1. Installée en 1.0, dépôt en 1.1, mise à jour acceptée | L'application se ferme puis redémarre en 1.1 |
| 2. Installée en 1.1, dépôt en 1.1 | Aucune mise à jour proposée |
| 3. Partage inaccessible | Message d'erreur, l'application reste utilisable |
| 4. Mise à jour refusée | L'application continue en version installée |
| 5. ZIP inexistant dans `version.json` | Message d'erreur, l'application n'est pas fermée |

> **Capture : version avant la mise à jour (1.1)**
>
> <img width="789" height="597" alt="Capture d’écran 2026-11-02 141318" src="https://github.com/user-attachments/assets/e785c887-95da-4c89-a450-e82fce4366ab" />

> **Capture : détection de la nouvelle version**
>
> <img width="783" height="593" alt="Capture d’écran 2026-11-02 140830" src="https://github.com/user-attachments/assets/36f99e60-de8a-4d81-b8e5-bd4d70efb507" />

> **Capture : version après la mise à jour (2.0)**
>
> <img width="1222" height="771" alt="Capture d’écran 2026-11-02 140944" src="https://github.com/user-attachments/assets/d201323f-436f-4901-8da2-01a25acb237c" />

# Réalisé en collaboration avec : `Antoine V.`
