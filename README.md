# VRC-Inventory
Inventory system generator for Avatar 3.0

Basic script to generate inventory systems for your avatar for use with avatar 3.0.

Setup:
1. Install the Avatar 3.0 SDK and setup the basic avatar descriptor with all of the default control layers
2. Install package from the most recent [release](https://github.com/Merlin-san/VRC-Inventory/releases/latest)
3. Add the Inventory Descriptor component to your avatar
4. Set the side of the Inventory Slots to the number of inventory items you want
5. Optionally choose a name for each slot that will show in the UI and an icon for that slot
6. Add the items under your avatar root that you want to toggle with the inventory to the **Slot Toggle Items** array
7. Click the **Generate Inventory** button on the bottom of the component
8. Before uploading delete the inventory descriptor because the SDK currently blocks uploads when it is on the avatar. This will hopefully be fixed soon.
