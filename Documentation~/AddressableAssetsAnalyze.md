# Addressables Analyze
Analyze is a tool featured in Addressables that gathers information on a projects' Addressables layout.  In some cases Addressables Analyze can take appropriate actions to clean up the state of a project.  In other cases Analyze is purely an informational tool that lets users make more informed decisions about their Addressables layout.

## How Analyze is Structured
Analyze is made up of `AnalyzeRule` objects.  Each of these objects can have an Analyze, a Fix, and a Clean step.

### Analyze Step
The analyze step of any `AnalyzeRule` is the informational step of the rule.  When running this action on a rule or set of rules, data can be gathered about the build, dependency maps, and more.  Each rule is responsible for gathering the desired data and reporting that back as a list of `AnalyzeResult` objects.

No action should be taken to modify any data or the state of the project in the Analyze step.  

For some rules, the **Fix** step can be an appropriate course of action given the data gathered in this step.  For others, like the _Check Scene to Addressable Duplicate Dependencies_ and _Check Resources to Addressable Duplicate Dependencies_ rules, they only contain an Analyze step as no reasonably appropriate and universal action can be taken given information gathered by those rules.

Analyze Rules that are purely informational and contain no Fix operation are under the category **Unfixable Rules**.  Those that do have a Fix operation are under **Fixable Rules**

### Fix Step
For **Fixable Rules** you may choose to run the Fix operation.  This step will use data given to it during the Analyze Step and perform any necessary modifications to resolve the issues.  

For example, _Check Duplicate Bundle Dependencies_ is a fixable rule because there is a reasonably appropriate action that can be taken to resolve the issues detected in the Analyze Step.

### Clear Step
The Clear Step will remove any data gathered by the Analyze Step and update the `TreeView` accordingly.

## Provided Analyze Rules
### Check Duplicate Bundle Dependencies
This Analyze Rule checks for potentially duplicated assets.  It does so by scanning all groups with BundledAssetGroupSchemas, and spies on the planned asset bundle layout.  This requires essentially triggering a full build, so this check is time consuming and performance intensive.  

Duplicated assets are caused by assets in different bundles sharing dependencies.  An example would be marking two prefabs that share a material as addressable in different groups.  That material (and any of its dependencies) will be pulled into the bundles with each prefab.  To prevent this, the material has to be marked as addressable, either with one of the prefabs, or in its own space.  Doing so will put the material and its dependencies in a separate bundle.  

If this check finds any issues, and the Fix operation is run on this rule, a new group will be created, and all dependent assets will be moved into that group.

#### Cases to Ignore Duplicates
There is one scenario in which this removal of duplicates will be incorrect.  If you have an asset containing multiple objects, it is possible for different bundles to only be pulling in portions of the asset (some objects), and not actually duplicate.  An example would be an FBX with many meshes.  If one mesh is in BundleA and another is in BundleB, this check will think that the FBX is shared, and will pull it out into its own bundle.  In this rare case, that was actually harmful as neither bundle had the full FBX asset.

It is also worth noting that duplicate assets may not always be an issue.  If a situation arises where assets would never be requested by the same set of users, such as region specific assets, then duplicate dependencies may be desired or at least inconsequential.  Each project is unique and fixing duplicate assets dependencies should be evaluated on a case by case basis.

### Check Resources to Addressable Duplicate Dependencies
This Analyze Rule is used to detect if any assets, or asset dependencies, are duplicated between built Addressable data and assets placed into a `Resources` folder.  When these duplications are detected it means that data will be included into both the player build and Addressables.

This rule is marked as unfixable because no appropriate action can be taken.  It is a purely information step and the user will need to decide how to proceed and what action to take, if any.

One example of a possible manual fix would be to move the offending asset(s) out of Resouces and make them Addressable.

### Check Scene to Addressable Duplicate Dependencies
This Analyze Rule detects any asset, or asset dependencies, shared between the scenes in the Editor scene list and Addressables.  When these duplications are detected it means that data will be included into both the player build and Addressables.

This rule is marked as unfixable because no appropriate action can be taken.  It is a purely information step and the user will need to decide how to proceed and what action to take, if any.

One example of a possible manual fix would be to pull the built in scene(s) with duplicated references out of the Build Settings and make it an Addressable scene.

## Extending Analyze
Not every project is the same and some will require additional Analyze Rules that aren't packaged with Addressables.  In that event, creating your own `AnalyzeRule` maybe be required.

### The AnalyzeRule
Create a new class child class of `AnalyzeRule`.  The properties you'll want to `override` are: `CanFix` and `ruleName`.  `CanFix` tells Analyze if it is classed as a Fixable rule or not and `ruleName` is the display name you'll see that rule as in the Analyze Window.

The three methods to `override` are: ` List<AnalyzeResult> RefreshAnalysis(AddressableAssetSettings settings)`, `void FixIssues(AddressableAssetSettings settings)`, and `void ClearAnalysis()`.

If your rule is going to be categorized as "Unfixable" you don't have to override `FixIssues`.

#### RefreshAnalysis
This is your Analyze Step.  In this method, perform any calculations you'd like and cache any data you might need for a potential Fix step.  The return value is a `List<AnalyzeResult>`.  After you'd gathered your data, for each entry in your analysis, create a new `AnalyzeResult` with the data/information as a string for the first parameter and a `MessageType` for the second should you need to elevate this message type to either Warning or Error.  Return the list of objects you create.

If you need to make child elements in the `TreeView` for a particular `AnalyzeResult`, you can delineate the parent item and any children with `kDelimiter`.  Include the delimiter between the parent and child items.

#### FixIssues
This is your Fix Step.  After running your Analyze Step, if you decide there can be appropriate action(s) to take to resolve the issues, this is where you'll do that operation.

#### ClearAnalysis
This is your Clear Step.  Any data you cached in the Analyze Step can be cleaned and/or removed in this step.  The `TreeView` will update to reflect the lack of data.

#### Adding Custom Rules to the GUI
To get a custom rule to show up in the Analyze window, they must register themselves with the GUI class.  They do this through `AnalyzeWindow.RegisterNewRule<RuleType>()`.  The recommended pattern is:
```
class MyRule : AnalyzeRule {}
[InitializeOnLoad]
class RegisterMyRule
{
    static RegisterMyRule()
    {
        AnalyzeWindow.RegisterNewRule<MyRule>();
    }
}
```
