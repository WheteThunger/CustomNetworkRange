## Features

- Allows customizing the range at which entities are networked to players

## How it works

In vanilla Rust, the map consists of an invisible grid of cells called "network groups". Each entity resides a positional network group (with some exceptions), and each player (a.k.a. subscriber) is subscribed to a certain number of network groups around them, based on proximity.

- When a player moves around the map, they automatically subscribe to and unsubcribe from network groups based on their location
- When an entity changes in any way, it broadcasts an update to all subscribers

The primary purpose of this plugin is to allow you to reduce network range. This effectively reduces the range that players will be able to see entities (draw distance). This has several advantages.

- Improves client performance since fewer entities need to be processed or rendered
- Improves server performance by reducing the quantity of network updates
  - When a player spawns in or teleports to a particular location, fewer entities need to be sent to them
  - When an entity changes, the update is sent to fewer subscribers
- Cheaters aren't able to shoot players across the map as far

## Configuration

Default configuration (vanilla equivalent):

```json
{
  "VisibilityRadiusFar": 8,
  "VisibilityRadiusNear": 4
}
```

- `VisibilityRadiusFar` (vanilla: `8`) -- Determines the range (in cells) that a player can subscribe to nearby network groups. Think of this as how far players can see entities.
- `VisibilityRadiusNear` (vanilla: `4`) -- Determines the range (in cells) for the following two things:
  - High priority network groups. When a player moves between network groups, entities in closer network groups will be sent to the player first.
  - Range that a remote-controled entity such as a CCTV or drone can see.
