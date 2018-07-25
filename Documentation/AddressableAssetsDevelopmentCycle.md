# Addressable Assets development

One of the key benefits of Addressables Assets is the decoupling of how you arrange your content, how you build your content, and how you load your content. Traditionally these facets of development are heavily tied together. If you arrange your content into Resources directories, the content is built into the base player, and you must load the content by calling the [Resources.Load](https://docs.unity3d.com/ScriptReference/Resources.Load.html) method and supplying the path to the resource.

To access content stored elsewhere, you used direct references or Asset bundles. If you used Asset bundles, you again loaded by path, tying your loading and your arranging together. If your Asset bundles were remote, or have dependencies in other bundles, you had to write a code to manage downloading, loading, and unloading of all of your bundles.

Giving an Asset an address, allows you to load it by that address, no matter where it is in your Project or how it was built.  You can change an Asset’s path or filename without problem.  You can also move the Asset from Resources, or from a local build destination, to some other build location, including remote ones, without your loading code ever changing.

## Play Mode Iteration

Addressable Assets has three Play Modes to help you accelerate your app development.

Fast mode allows you to run the game quickly as you work through the flow of your game. Fast mode loads Assets directly through the Asset Database for quick iteration with no analysis or Asset bundle creation.

Virtual mode analyzes content for layout and dependencies without creating Asset bundles. Assets load from the Asset Database though the ResourceManager as if they were loaded through bundles. You can view Asset usage in the ResourceManager (RM) Profiler Window to see when bundles load/unload during game play.

Virtual mode helps you simulate load strategies and tweak your content groups to find that right balance for a production release.

Packed mode fully packs content and creates Asset bundles on disk. This mode takes the most time to prepare and provides the most accurate behavior for resource loading.

Each mode has its own time and place during development and deployment.

The following table shows segment of the development cycle in which a particular mode is useful.

| | Design | Develop | Build | Test / Play | Publish |
|:---|:---|:---|:---|:---|:---|
| Fast| x | x |   | x In Editor only |   |
|
Virtual| x | x | x Asset Bundle Layout | x In Editor only |  |
| Packed|   |   | x Asset Bundles  | x | x |