# MINDRIFT

MINDRIFT est un prototype action-game Unity (HDRP) avec une ambiance cyber/psyche, un main menu stylise, des options completes, une couche online API (PHP JSON), et une base Discord Rich Presence.

## Environnement

- Unity: `6000.3.10f1`
- Pipeline: `HDRP`
- Input: `Input System` (pas d ancien Input Manager)
- Plateforme cible: PC (`clavier/souris + manette`)

## Demarrage rapide

1. Ouvrir le projet dans Unity.
2. Verifier les scenes dans `Build Settings`:
   - `Assets/Scenes/MainMenu.unity`
   - `Assets/Scenes/Games.unity`
   - `Assets/Scenes/Break.unity`
3. Lancer depuis `MainMenu`.

## Flux des scenes

- `MainMenu`: navigation principale (`Play / Options / Quit`), login, leaderboard.
- `Games`: gameplay principal.
- `Break`: menu pause (charge additivement).

## Fonctionnalites principales

### Main Menu / UX

- Curseur force visible + deverrouille dans le menu.
- Layout ergonomique (titre centre en haut, actions au centre, panels lateraux).
- `LeaderBoard` alimente par les donnees API reelles (plus de seed fake locale dans la liste principale).
- Panneau account oriente connexion API.
- `FooterHints` masque dans le MainMenu.

### Menu Options

- Onglets: `Audio`, `Display`, `Controls`.
- Audio: `Master`, `Music`, `SFX`.
- Display: `Fullscreen`, `Quality`.
- Controls: `Controller Sensitivity`, `Invert Y`, `Controller Vibration`.
- Navigation clavier/manette/souris supportee.

### Gameplay / Systeme

- Vibration manette declenchee a la mort (si activee).
- Limite de vies conservee cote gameplay.
- Systeme de stats locales toujours present.

### Online (nekuzaky API)

Architecture modulaire sous `Assets/_MINDRIFT/Scripts/Online/`:

- `Core`: `ApiConfig`, `ApiClient`, `ApiRoutes`, `JsonHelper`
- `Auth`: `AuthManager`, `TokenStorage`, `SessionBootstrap`
- `Models`: DTO login/user/stats/settings/run/leaderboard
- `MindriftOnlineService`: facade online unique

Capacites actuelles:

- Login token-based (`email + mot de passe`)
- Stockage token local + restauration de session via `/auth/me.php`
- Fetch profil / stats / leaderboard
- Fetch + save des settings
- Envoi de run (`save-run`)

## Configuration API

### Base URL

- Defaut: `https://nekuzaky.com/api`
- Fichier: `Assets/_MINDRIFT/Scripts/Online/Core/ApiConfig.cs`

### Surcharge via asset (recommande)

Creer un asset:

1. `Create > MINDRIFT > Online > API Config`
2. Nommer exactement l asset: `MindriftApiConfig`
3. Le placer dans: `Assets/Resources/`

Sans asset, un fallback runtime est utilise.

### Endpoints centralises

- `auth/login.php`
- `auth/me.php`
- `mindrift/my-stats.php`
- `mindrift/leaderboard.php`
- `mindrift/settings-get.php`
- `mindrift/settings-save.php`
- `mindrift/save-run.php`

## Discord Rich Presence

- Config ScriptableObject: `MindriftDiscordPresenceConfig`
- Asset attendu: `Assets/Resources/MindriftDiscordPresenceConfig.asset`
- Application ID deja configure dans le projet.
- Le code compile en mode SDK uniquement avec le define:
  - `MINDRIFT_DISCORD_SDK`

## Controles

### MainMenu

- Clavier: `WASD` / fleches + `Enter`
- Manette: stick/D-pad + `A`
- Souris: navigation/clic UI

### Gameplay / Pause

- `Esc` (clavier) ou `Start` (manette): pause/reprise

## Contrat backend attendu (resume)

Reponse API attendue:

```json
{
  "success": true,
  "message": "OK",
  "data": {}
}
```

Le login doit retourner un token exploitable (`token`, `access_token`, etc.) et idealement un user avec identite (`display_name` ou `username`) pour eviter l affichage generique.

## Scripts cles

- `Assets/_MINDRIFT/Scripts/UI/MainMenuController.cs`
- `Assets/_MINDRIFT/Scripts/UI/OptionsMenuController.cs`
- `Assets/_MINDRIFT/Scripts/UI/SettingsManager.cs`
- `Assets/_MINDRIFT/Scripts/Online/Auth/AuthManager.cs`
- `Assets/_MINDRIFT/Scripts/Online/MindriftOnlineService.cs`
- `Assets/_MINDRIFT/Scripts/Online/Presence/DiscordRichPresenceManager.cs`
