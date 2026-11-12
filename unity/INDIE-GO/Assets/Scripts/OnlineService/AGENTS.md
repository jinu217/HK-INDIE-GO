# Online service development

- Work on `dev/PhotonFusion` for online features. Check the current branch before editing.
- Put every new online script, asset and document under `Assets/Scripts/OnlineService/`.
- Keep `ThirdParty/Photon/` vendor files unchanged unless SDK maintenance is requested.
- Implement internal services only. Do not create or modify scenes, panels, prefabs or scene objects, including via commands or runtime bootstrap code.
- NetworkRunner ownership stays with the external integration layer. No automatic GameObject/component creation in this layer.
- Preserve existing `RoomSettingsData`, custom `playerTeams`, and both team mode semantics.
- Run `Validation~/Run-Validation.ps1` after meaningful logic changes. Distinguish local behavior tests/SDK compilation from actual Photon Cloud testing.
