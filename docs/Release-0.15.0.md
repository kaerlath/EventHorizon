# Event Horizon 0.15.0

- Added **Stay connected while playing** in Connection settings, enabled by default. Discord stays linked while logged into FFXIV, including with the planner closed or game UI hidden.
- Game logout or plugin unload disconnects. Sessions expire after 15 minutes without renewal if the game crashes or loses contact. Turning the option off restores sessions of up to one hour.
- Discord authorization renews automatically when necessary. Revoked authorization still requires reconnecting. Offline Google Calendar and Discord reminder jobs continue independently.
- Updated the in-game walkthrough and player guide.
- Includes the relay's compact landscape announcement artwork with complete, readable event details in Discord message text. Save & Sync updates existing posts.

Deploy the updated Cloudflare relay before installing this plugin update. Reconnect Discord once after updating. No new secrets or permissions are needed.

Validation: release build and automated relay/.NET checks passed. Live game logout and Cloudflare deployment remain to be verified on the operator's computer.
