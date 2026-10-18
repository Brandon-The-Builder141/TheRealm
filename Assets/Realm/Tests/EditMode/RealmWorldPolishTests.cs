using System;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheRealm.Tests
{
    public sealed class RealmWorldPolishTests
    {
        Scene scene;
        GameObject root;
        Func<string,GameObject> previousKit;
        Func<string,Material> previousMaterials;

        [SetUp] public void Setup()
        {
            previousKit = RealmArt.KitProvider;
            previousMaterials = RealmArt.MaterialProvider;
            scene = EditorSceneManager.NewPreviewScene();
            root = new GameObject("Polish test environment");
            SceneManager.MoveGameObjectToScene(root, scene);
            Surface("The frontier", new Vector3(0,-.6f,213), new Vector3(100,1,490));
        }

        [TearDown] public void Teardown()
        {
            RealmArt.KitProvider = previousKit;
            RealmArt.MaterialProvider = previousMaterials;
            EditorSceneManager.ClosePreviewScene(scene);
        }

        GameObject Surface(string name, Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        [Test] public void FootprintOverlapRejectsCentreOutsideRoad()
        {
            Assert.IsTrue(RealmWorldPolish.OverlapsXZ(new Bounds(new Vector3(4.5f,0,50),new Vector3(6,0,6)),
                new Bounds(new Vector3(0,0,208),new Vector3(7,0,451))));
        }

        [Test] public void SamplesVisibleGroundInsteadOfLogicalZero()
        {
            var placement = new RealmWorldPolish.Placement(root.transform, Array.Empty<QuestDefinition>());
            Assert.IsTrue(placement.SampleSurface(new Vector3(12,0,50),out float height));
            Assert.That(height, Is.EqualTo(-.1f).Within(.0001f));
            Assert.IsFalse(placement.SampleSurface(new Vector3(70,0,50),out _));
        }

        [Test] public void FootprintCrossingRaisedRoadIsRejected()
        {
            Surface("The old road",new Vector3(0,-.065f,208),new Vector3(7,.08f,451));
            var placement = new RealmWorldPolish.Placement(root.transform, Array.Empty<QuestDefinition>());
            var bounds = new Bounds(new Vector3(4,0,50),new Vector3(4,0,4));
            Assert.IsFalse(placement.IsClear(bounds));
            Assert.IsFalse(placement.FootprintHeight(bounds,out _));
        }

        [Test] public void QuestProtectionIncludesEntireInteractionRadiusAndFootprint()
        {
            var placement = new RealmWorldPolish.Placement(root.transform,new[]{new QuestDefinition{location=new Vector3(12,0,50)}});
            Assert.IsFalse(placement.IsClear(new Bounds(new Vector3(18,0,50),new Vector3(4,0,4))));
            Assert.IsTrue(placement.IsClear(new Bounds(new Vector3(21,0,50),Vector3.one)));
        }

        [Test] public void ExistingRootIsPreservedWithoutCallingProvider()
        {
            var existing = new GameObject(RealmWorldPolish.RootName);
            existing.transform.SetParent(root.transform,false);
            RealmArt.KitProvider = _ => throw new Exception("Must not create assets on repeat");
            Assert.AreSame(existing,RealmWorldPolish.Build(root.transform));
        }

        [Test] public void TreeBaseExceptionDoesNotBypassQuestClearance()
        {
            var tree = Surface("Oak",new Vector3(12,1,50),Vector3.one);
            tree.AddComponent<BoxCollider>();
            var bounds = new Bounds(new Vector3(12,0,50),new Vector3(2,0,2));
            var placement = new RealmWorldPolish.Placement(root.transform,Array.Empty<QuestDefinition>());
            Assert.IsFalse(placement.IsClear(bounds));
            Assert.IsTrue(placement.IsClear(bounds,true));
            var protectedPlacement = new RealmWorldPolish.Placement(root.transform,new[]{new QuestDefinition{location=new Vector3(12,0,50)}});
            Assert.IsFalse(protectedPlacement.IsClear(bounds,true));
        }

        [Test] public void DuplicateRootsFailWithoutRemovingEither()
        {
            for(int i=0;i<2;i++) new GameObject(RealmWorldPolish.RootName).transform.SetParent(root.transform,false);
            Assert.Throws<InvalidOperationException>(()=>RealmWorldPolish.Build(root.transform));
            Assert.AreEqual(3,root.transform.childCount);
        }

        [Test] public void MissingAssetRollsBackOnlyNewRoot()
        {
            RealmArt.KitProvider = _ => null;
            Assert.Throws<InvalidOperationException>(() => RealmWorldPolish.Build(root.transform));
            Assert.IsNull(root.transform.Find(RealmWorldPolish.RootName));
            Assert.IsNotNull(root.transform.Find("The frontier"));
        }

        [Test] public void SlopedSurfaceFailsInsteadOfUsingBoundingBoxTop()
        {
            root.transform.Find("The frontier").localRotation = Quaternion.Euler(5,0,0);
            Assert.Throws<InvalidOperationException>(() => new RealmWorldPolish.Placement(root.transform,Array.Empty<QuestDefinition>()));
        }
    }
}
