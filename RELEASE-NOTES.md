## Going Public Server 1.0.16.2

**Server and players both need 1.0.16.2.** Please update the Workshop mod and the server together - the server only lets the same version in.

Fixes after the test of 1.0.16.1:

### Radio
- **Internet stations play now**: in 1.0.16.1 every stream stayed silent, whatever the station, in the car too. The game engine cannot play an endless MP3 stream. The mod now downloads and decodes the streams itself (with NLayer, MIT licence, in the `Dependencies/` folder).
- **http and https**: both kinds of station address play. The "https only" limit of 1.0.16.1 is gone.
- **Song titles**: the song that plays shows under the station's name when the station sends it (in the Radio tab, in the phone's radio and at a loudspeaker).
- **Radio on foot**: the Radio tab has a "Radio on foot" switch. When it is on, the radio keeps playing out of the car, also without headphones. Inside buildings it is silent; the loudspeakers play there. The switch applies to your own computer only.
- **Shop loudspeakers**: a shop's loudspeakers play internet stations too.
  - In your own shop the Radio tab shows "My radio" and "Shop loudspeakers" at the top; "Shop loudspeakers" is preselected. Tap a station and the loudspeakers play it.
  - At a loudspeaker itself (mouse on it), "Next station" goes on from the game's stations into the internet stations.
  - Everybody in the shop hears the same station, and the server keeps it with the world.
  - The shop's owner, his merger partners and holders of his Business key may choose.
- **Passengers** hear the driver's station, internet stations too. After getting out their own station plays again, an internet station too. With "Radio on foot" it stays audible.
- The English station list now includes TruckersFM.

### Server console
- **Server key and version** show in AMP again. The line used to come only before the world loaded, and AMP cleared it once the world was up. Now it comes once more when the world runs, then every 10 minutes.

### Other
- The version line at the bottom left reads "GPS-MM v. 1.0.16.2".
- Everything from 1.0.16.1 is included (self-driving, passengers, Radio tab, sitting, merger, quieter console).
