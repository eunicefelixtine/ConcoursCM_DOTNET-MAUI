# ConcoursCM

> Application mobile **.NET MAUI** (Android) qui centralise les **concours d'entrée** et les **bourses d'études officiels du Cameroun**, aide les candidats à préparer leur **dossier physique** et leur envoie des **rappels de dates limites**, y compris sans connexion internet.

---

## 1. Contexte et objectifs

Au Cameroun, les informations sur les concours (ENSPY, FMSB, ENAM…) et sur les bourses sont dispersées entre arrêtés ministériels, sites d'écoles et réseaux sociaux. Les candidats risquent de manquer une date limite, d'oublier une pièce du dossier physique, ou de tomber sur de faux intermédiaires qui demandent de l'argent.

**ConcoursCM répond à trois besoins :**

| Besoin | Réponse de l'application |
|---|---|
| Trouver les offres officielles | Catalogue de concours et de bourses, avec recherche, filtres et vérification du lien officiel |
| Ne rien oublier | Checklist du dossier physique, avancement en %, fiche récapitulative PDF, rappels J-7 / J-3 / J-1 |
| Éviter les arnaques | Aucun paiement dans l'application, publication réservée aux institutions accréditées, règles strictes sur les liens |

L'application est aussi pensée pour les **réseaux 2G/3G** : les données sont stockées localement et les rappels fonctionnent hors connexion.

Les écrans suivent les maquettes fournies (12 écrans) et l'arborescence de navigation associée.

---

## 2. Fonctionnalités

### Côté candidat
- **Inscription / connexion** par e-mail ou numéro camerounais, avec indicateur de robustesse du mot de passe.
- **Accueil** : salutation, recherche rapide, compteurs (concours ouverts, bourses actives), bloc « À la Une » des échéances proches.
- **Liste des concours** : recherche sans accents, filtres par catégorie, tri (clôture, récents, A–Z), option « compatibles avec mon niveau », favoris.
- **Liste des bourses** : recherche, catégories (Excellence, Coopération, Master/Doctorat…), favoris.
- **Détail d'un concours** : date limite, épreuves, places, frais, arrêté officiel, pièces requises, éligibilité, bouton vers le portail officiel.
- **Checklist du dossier physique** : cases à cocher persistantes, barre de progression, lieu et heure de dépôt, génération d'une **fiche récapitulative PDF** partageable.
- **Mes candidatures suivies** : tous les dossiers en cours avec leur avancement.
- **Centre d'alertes** : rappels, nouveaux arrêtés, bourses, état du service de rappels ; marquage lu / non lu.
- **Profil et paramètres** : niveau d'études, numéro de rappel, alertes SMS, mode Low Data, thème clair/sombre, aide, déconnexion.

### Côté institution (école, université)
- **Inscription avec accréditation** : établissement, tutelle, représentant habilité, e-mail institutionnel, téléphone, PDF de mandat.
- **Publication d'annonces officielles** : concours ou bourse, avec arrêté PDF, dates, places, frais et lien du portail.
- **Suivi des annonces** dans le profil (En vérification, Publié, Rejeté).

---

## 3. Stack technique et justification des choix

### Plateforme et langage

| Élément | Choix | Raison |
|---|---|---|
| Framework | **.NET MAUI** (cible `net10.0-android`) | Demande du projet. Un seul code C#/XAML, extensible à iOS en ajoutant `net10.0-ios` |
| Langage | **C# (LangVersion latest)** avec `Nullable` et `ImplicitUsings` activés | Moins d'erreurs grâce aux références nulles vérifiées ; moins de code répétitif |
| UI | **XAML** | Interface déclarative, liaisons de données, thèmes clair/sombre |
| SDK | **.NET 10** | Le projet cible .NET 10 (`net10.0-android`) avec `Microsoft.Maui.Controls` 10.0.20 |
| Android minimum | **API 24 (Android 7.0)** | Couvre la grande majorité des téléphones utilisés localement, y compris les modèles d'entrée de gamme |

### Paquets NuGet

| Paquet | Version | Rôle | Pourquoi ce choix |
|---|---|---|---|
| `Microsoft.Maui.Controls` | 10.0.20 | Contrôles et moteur de MAUI, version alignée sur .NET 10 | Fournit Shell, pages, XAML ; la version doit correspondre au SDK .NET 10 |
| `CommunityToolkit.Mvvm` | 8.3.2 | Génère le code MVVM (`[ObservableProperty]`, `[RelayCommand]`) | Supprime des centaines de lignes de code répétitif, très répandu et bien maintenu |
| `CommunityToolkit.Maui` | 9.1.1 | Toasts et convertisseurs (`IsStringNotNullOrEmptyConverter`, `InvertedBoolConverter`) | Messages discrets (toast) sans bibliothèque supplémentaire |
| `sqlite-net-pcl` | 1.9.172 | Accès à la base **SQLite** locale | ORM léger et asynchrone, adapté à une base embarquée sur mobile |
| `SQLitePCLRaw.bundle_green` | 2.1.10 | Moteur natif SQLite pour Android | Dépendance nécessaire à `sqlite-net-pcl` pour fonctionner sur appareil |
| `Plugin.LocalNotification` | 11.1.4 | Notifications locales planifiées | Les rappels s'affichent **sans internet** et sans serveur de notification |
| `QuestPDF` | 2024.12.3 | Génération de la fiche récapitulative PDF | API simple et lisible ; licence Community gratuite sous conditions (voir §13) |
| `Microsoft.Extensions.Logging.Debug` | 10.0.0 | Journalisation en mode Debug | Aide au diagnostic pendant le développement |

---

## 4. Architecture

Le projet applique le patron **MVVM** (Model – View – ViewModel) avec **injection de dépendances**.

```
┌──────────┐  binding   ┌──────────────┐  appelle   ┌────────────┐   lit/écrit   ┌─────────┐
│  Views   │ ─────────► │  ViewModels  │ ─────────► │  Services  │ ────────────► │ SQLite  │
│ (XAML)   │ ◄───────── │ (état + cmd) │ ◄───────── │ (métier)   │               │ + Prefs │
└──────────┘            └──────────────┘            └────────────┘               └─────────┘
```

| Couche | Responsabilité | Règle suivie |
|---|---|---|
| **Views** (`Views/`, `Controls/`) | Affichage uniquement | Aucune logique : le code-behind se limite à charger les données à l'apparition de la page |
| **ViewModels** (`ViewModels/`) | État de l'écran et commandes | Aucun accès direct à la base ; ils appellent les services |
| **Services** (`Services/`) | Toute la logique métier | Testables indépendamment de l'interface |
| **Models** (`Models/`) | Entités persistées et énumérations | Tables SQLite et propriétés calculées (jours restants, urgence…) |
| **Helpers** (`Helpers/`) | Fonctions utilitaires | Formats, validation, hachage |

### Injection de dépendances (`MauiProgram.cs`)
- **Services en singleton** : une seule connexion SQLite, un état de session partagé.
- **Pages et ViewModels en transient** : un nouvel exemplaire à chaque ouverture, donc un état propre.
- La méthode `AddPage<TPage, TVm>()` enregistre chaque couple page/ViewModel en une ligne.

### Choix de conception notables
- **`NavigationService`** : les ViewModels naviguent et affichent des dialogues sans dépendre de `Shell` directement, ce qui les rend plus simples à tester.
- **`ISmsGateway`** : interface qui isole l'envoi de SMS. Aujourd'hui simulé (`PlaceholderSmsGateway`) ; il suffira d'écrire une implémentation réelle plus tard.
- **`SearchContext`** : transmet une recherche saisie sur l'accueil vers l'onglet Concours.
- **Recherche différée (300 ms)** : évite de relancer la requête à chaque lettre tapée.
- **Types `…Vm` (`Items.cs`)** : chaque ligne de liste est enveloppée dans un petit ViewModel qui prépare les textes et couleurs, ce qui garde les modèles propres.

---

## 5. Logique métier détaillée

### 5.1 Authentification (`AuthService`)
- **Identifiants normalisés** : e-mail en minuscules ; téléphone converti en `+237XXXXXXXXX` (acceptant espaces, points, tirets, avec ou sans indicatif).
- **Mot de passe** : minimum 6 caractères (candidat), 8 avec lettres **et** chiffres (institution).
- **Verrouillage** : après 5 échecs, connexion bloquée 5 minutes.
- **Message d'erreur volontairement générique** (« Identifiant ou mot de passe incorrect ») pour ne pas révéler quels comptes existent.
- **Séparation des rôles** : un compte institution ne peut pas se connecter par l'onglet Candidat, et inversement, avec un message explicatif.

### 5.2 Catalogue (`ConcoursService`)
- **Recherche tolérante** : insensible à la casse et aux accents (`Fmt.Normaliser`).
- **Filtres** : catégorie, concours ouverts uniquement, compatibles avec mon niveau.
- **Tri** : par date de clôture (les concours clos passent en dernier), par date de publication, alphabétique.
- **« À la Une »** : concours et bourses encore ouverts qui se terminent dans les 30 jours, les plus proches d'abord.
- **Éligibilité** : le niveau du profil (ex. *Bac C*) est comparé aux niveaux acceptés par le concours. Résultat : Éligible, Non éligible ou Inconnue (profil incomplet).

### 5.3 Urgence des échéances
La clôture est calculée à l'**heure précise** (ex. 15h30), pas seulement au jour.

| Jours restants | Niveau | Affichage |
|---|---|---|
| Dépassé | Fermé | Badge gris « Clos » |
| ≤ 3 | Critique | Rouge |
| ≤ 7 | Urgent | Rouge |
| ≤ 30 | Proche | Orange |
| > 30 | Aucun | Bleu |

### 5.4 Checklist (`ChecklistService`)
- Les lignes de checklist sont créées à la première ouverture ; si l'institution ajoute une pièce ensuite, elle apparaît automatiquement.
- Quand toutes les pièces sont cochées : message « Dossier complet ✓ » avec lieu et heure de dépôt.
- Si le concours est clos, la checklist l'indique.
- Suivre ou cocher une pièce **recalcule les rappels**.

### 5.5 Rappels (`AlerteService`)
- Pour chaque concours **suivi ou mis en favori**, et chaque bourse favorite :
  1. des **notifications locales** sont planifiées à 8h, J-7, J-3 et J-1 (selon les seuils activés) ;
  2. une **alerte** est ajoutée au centre d'alertes au plus petit seuil atteint, avec le nombre de pièces manquantes.
- **Pas de doublon** : chaque alerte a une clé unique (`rappel-c{concours}-u{utilisateur}-J{seuil}`).
- **Identifiants de notification stables** : un hash FNV-1a est utilisé, car `string.GetHashCode()` change à chaque lancement et créerait des doublons.
- **Annonces officielles** : quand une annonce est validée, tous les candidats reçoivent une alerte « Arrêté officiel ».
- Les SMS passent par `ISmsGateway` (simulé pour l'instant).

### 5.6 Publication (`PublicationService`)
Règles appliquées avant d'accepter une annonce :

| Règle | Détail |
|---|---|
| Accréditation | Seule une institution **validée** peut publier |
| Intitulé | Au moins 10 caractères |
| Date limite | Au moins 3 jours après aujourd'hui |
| Épreuves | Après la clôture des dépôts |
| Places / frais | Places entre 1 et 100 000 ; frais entre 0 et 200 000 FCFA |
| Lien | HTTPS, domaine `.cm` ou domaine de l'e-mail institutionnel, ni IP, ni lien raccourci/réseau social |
| Arrêté | PDF obligatoire, 8 Mo maximum |
| Doublon | Même intitulé la même année refusé |

**Modération simulée** : l'annonce passe de *En attente* à *Publié*. En **Debug**, la validation est immédiate pour faciliter les tests ; en **Release**, elle attend **24 h**, comme annoncé dans l'interface.

### 5.7 Fiche récapitulative (`FicheRecapService`)
Génère un PDF A4 avec : candidat, concours, tutelle, quittance, date limite, lieu de dépôt, avancement, liste des pièces cochées/à faire, rappel anti-intermédiaire. Le fichier est ensuite partagé via la feuille de partage Android (WhatsApp, e-mail, impression…).

---

## 9. Sécurité et lutte contre la fraude

| Mesure | Où | Pourquoi |
|---|---|---|
| Hachage **PBKDF2-SHA256**, 100 000 itérations, sel aléatoire de 16 octets | `PasswordHasher` | Un mot de passe volé de la base n'est pas réutilisable |
| Comparaison en **temps constant** | `FixedTimeEquals` | Évite les attaques par mesure de temps |
| Verrouillage temporaire après 5 échecs | `AuthService` | Freine les essais répétés |
| Aucun paiement dans l'application | Conception | Supprime le risque d'arnaque sur les frais |
| Avertissement avant d'ouvrir un portail | `NavigationService.OuvrirLienOfficielAsync` | Rappelle de ne payer que sur le site officiel |
| Liens HTTPS uniquement | Ouverture + publication | Empêche les liens non sécurisés |
| E-mail institutionnel obligatoire | Inscription institution | Écarte les faux établissements |
| PDF copiés dans le stockage privé avec un nom aléatoire | `FichierService` | Évite les collisions et l'exposition du fichier d'origine |
| Taille et extension des PDF contrôlées | `FichierService` | Évite les fichiers volumineux ou d'un autre type |

---

## 10. Design system

Les styles reprennent les maquettes : bleu royal profond, cyan électrique, cartes blanches arrondies, badges en pastille.

- **Couleurs** (`Colors.xaml`) : `Primary #1E3A8A`, `PrimaryDeep #0B2A7A`, `Accent #0EA5E9`, fonds clair/sombre, alertes (rouge, ambre, vert, teal).
- **Styles** (`Styles.xaml`) : `Card`, `SoftBox`, `FieldBorder`, `Badge`, `PrimaryButton`, `SoftButton`, `SmallButton`, `OutlineButton`, `H1`, `H2`, `Muted`, `Caption`, `Link`.
- **Thème clair/sombre** : `AppThemeBinding` partout, commuté depuis le Profil.
- **Contrôles réutilisables** :
  - `FormField` : libellé, icône, indication, œil pour les mots de passe, message d'erreur ;
  - `SegmentedTabs` : sélecteur à deux onglets (Candidat/Institution, Clair/Sombre, Concours/Bourse) ;
  - `TopBar` : logo ou retour, titre, cloche avec compteur, avatar.
- **Icônes** : emojis (aucun fichier d'image à embarquer, donc application plus légère, adaptée au mode Low Data).
- **Icône et écran de démarrage** : SVG simples fournis dans `Resources/`, à remplacer par votre vrai logo.

---

## 11. Installation et lancement

### Prérequis
- **SDK .NET 10** (`dotnet --list-sdks` doit afficher une version 10.x).
- **JDK 17** et **SDK Android** (API 34 conseillée), installés via Android Studio ou Visual Studio.
- Charge de travail MAUI Android :
  ```bash
  dotnet workload install maui-android
  ```
- Un émulateur Android ou un téléphone avec le débogage USB activé.

### Visual Studio à jour (compatible .NET 10)
1. Ouvrir `ConcoursCM.csproj`.
2. Choisir l'émulateur ou le téléphone dans la barre d'outils.
3. Appuyer sur **F5**.

### VS Code
1. Installer les extensions **C# Dev Kit** et **.NET MAUI**.
2. Ouvrir le dossier du projet, puis dans le terminal :
   ```bash
   dotnet restore
   dotnet build -t:Run -f net10.0-android
   ```
3. Démarrer d'abord l'émulateur, ou brancher le téléphone (`adb devices` doit le lister).

Le premier build dure 3 à 6 minutes ; les suivants sont plus rapides.

---

Projet réalisé par : NZEUTEM DOMMOE Eunice Felixtine - 22G00347
