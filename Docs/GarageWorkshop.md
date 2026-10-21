# PC garage workshop

`Assets/Scenes/AssemblyWorkshop.unity` contains a custom 14 × 14 metre garage, with a 3.8 metre clear ceiling, solid walls and floor, a sectional door, windows, protective wall panels, steel roof beams, overhead lamps and a marked engine bay. The room uses separate material copies derived from the Simple Garage textures. Imported furnishings keep their own materials, converted to URP with their texture maps preserved.

Six fixed supply benches use the pack's table model. All assembly parts are staged on these waist-height surfaces, with spacing measured from each mesh's world bounds. Matching geometry groups stay consecutive. Long parts turn across the table. The engine, parts and socket frames now use 60% of their previous linear scale. Both sides of the table rows are accessible; the taller shelves are decorative tool storage and never supply assembly parts. The small portable work platform beside the engine remains movable.

`WorkshopSupplyLayout` belongs only to the PC workshop scene. It lays out the current chapter after the recipe has installed previous chapters. Subassembly carrier frames stay on their lifts, and assembly socket/recipe coordinates and mobile staging remain unchanged. Repair replacement and removed components use the same tables.

Outside Play mode, **Tools → Engine Assembly → Environment → Build Custom PC Garage** rebuilds the generated environment and supplies configuration. This replaces the named generated garage/lighting roots. Original meshes and the Simple Garage source scene are retained. The old slab's recipe coordinate frame remains but its visual and collider are disabled. A rollback copy of the original workshop and pack material files is in `Tools/Verification/BeforeGarageIntegration`.

The PC chapter selector and pause screen use a translucent backdrop so the game stays visible. Phone fallback scenes clear the complete screen behind their reserved engine viewport; tracked AR hides that extra background camera so the real camera feed remains visible.

The isolated Editor play verification checks all 317 physical supply parts across the chapters for supported bounds, standing pickup distance, clear approaches and pairwise non-overlap. It also completes the repair sequence, checks ground/ceiling lift limits, and renders landscape/portrait phone views and PC menus. Results are in `Tools/Verification/garage-phone-report.json`; previews are in `Docs/Previews/GarageWorkshop.png`, `GaragePause.png`, `GarageChapters.png`, `MobileDisplay-Light.png` and `MobileAssembly-Light.png`.
