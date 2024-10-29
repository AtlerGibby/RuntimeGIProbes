Runtime GI Probes is a full-screen image effect that adds global illumination to your scene by capturing the scene’s illumination anywhere a GI Probe is placed. Runtime GI Probes works independently from Unity’s built-in lighting and GI Probe solution.

Try out the [Demo](https://atlergibby.itch.io/runtime-gi-probes)

# Features
* Fast GI Baking. Can take seconds bake on mid to high end PCs.
* Debugging options for visualizing lightmaps.
* Supports VR multi-pass and single-pass rendering.
* GI probes are simple to create and move like any other game object.
* Supports perspective and orthographic cameras.
* Bounding Boxes for controlling where and how strong the GI is. Simple to move and scale like any other game object.
* Can support many cameras and can customize how each camera sees the GI.
* Compatible with other post processing effects.
* C# functions for manipulating lighting in real-time.
* Includes a Demo scene and PDF Documentation.

# Requirements
* Unity 2021.3 or later.
* Built for URP only.
* Only opaque / alpha cutout materials receive global illumination.
* Does not replace real-time lighting and shadows / requires URP’s SSAO or Deferred Rendering to be enabled. Therefore, it is not recommended for older mobile devices or Quest 2 standalone. Although, it is possible to run on modern mobile devices that support Vulkan at lower settings.
