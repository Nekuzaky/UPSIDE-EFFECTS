# Changelog

Toutes les evolutions notables de MINDRIFT sont listees ici.

Le format suit l esprit de [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/).

## [Unreleased] - 2026-03-27

### Added

- Couche online modulaire (`Assets/_MINDRIFT/Scripts/Online/`) avec client HTTP JSON, config centralisee, auth token, et service central.
- Base Discord Rich Presence avec bootstrap runtime, manager de presence, et config ScriptableObject.
- Menu options enrichi avec onglets `Audio`, `Display`, `Controls`.
- Reglage vibration manette persistant + vibration sur mort.

### Changed

- Main menu retravaille pour une structure plus ergonomique.
- Login oriente `email + mot de passe` cote flux UI/auth.
- Leaderboard du menu renomme en `LEADERBOARD`.
- Liste leaderboard du menu branchee sur les donnees API reelles (plus de seed fake locale dans la liste principale).
- Nettoyage visuel du main menu (`FooterHints` masque).

### Fixed

- Curseur souris force visible/deverrouille en menu.
- Correctifs de navigation menu/options (clavier/manette/souris).
- Prevention du lancement de musiques loop en main menu.
- Garde-fou contre la fenetre de jeu minuscule au lancement (`SettingsManager.RecoverFromTinyWindowIfNeeded`).
- Robustesse auth: message clair si token absent et extraction elargie des cles token.

### Notes

- Le backend doit continuer de respecter le format `success/message/data`.
- Les endpoints sont centralises dans `ApiRoutes.cs`.
- Pour un affichage de pseudo fiable, renvoyer `display_name` ou `username` cote API.
