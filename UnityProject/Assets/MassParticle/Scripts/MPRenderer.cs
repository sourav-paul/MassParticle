﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;


public unsafe class MPRenderer : MonoBehaviour
{
	public enum RenderMode {
		Plugin,
		Points,
		Cubes
	}

	MPMeshData meshData;
	List<GameObject> children;
	public RenderMode renderMode;
	public Material material;
	public float scale = 1.0f;
	public bool castShadows = true;
	public bool receiveShadows = true;
	RenderMode renderModePrev;

	const int dataTextureWidth = 3072;
	const int dataTextureHeight = 2048;
	RenderTexture dataTexture;
	GameObject meshes;
	Transform trans;
	Bounds bounds;
	MPWorld world;


	MPRenderer()
	{
	}

	void Start () {
		trans = gameObject.GetComponent<Transform>();
		world = gameObject.GetComponent<MPWorld>();
		meshes = new GameObject("mpMeshes");

		meshData = new MPMeshData();
		children = new List<GameObject>();
		dataTexture = new RenderTexture(dataTextureWidth, dataTextureHeight, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Default);
		dataTexture.isPowerOfTwo = false;
		dataTexture.filterMode = FilterMode.Point;
		dataTexture.Create();
		if (material)
		{
			material.SetTexture("_DataTex", dataTexture);
			material.SetFloat("_DataTexPitch", 1.0f / dataTextureWidth);
		}
	}

	void Update()
	{
		if (renderMode != renderModePrev)
		{
			foreach(GameObject child in children) {
				Destroy(child);
			}
			children.Clear();
		}
		renderModePrev = renderMode;

		Vector3 min = trans.position - trans.localScale;
		Vector3 max = trans.position + trans.localScale;
		bounds.SetMinMax(min, max);
		material.SetFloat("_ParticleSize", world.particleSize * scale);

		int num = MPNative.mpGetNumParticles();
		if (num == 0) { return; }

		switch (renderMode)
		{
			case RenderMode.Plugin:
				break;

			case RenderMode.Points:
				UpdatePointMeshes();
				MPNative.mpUpdateDataTexture(dataTexture.GetNativeTexturePtr());
				break;
			case RenderMode.Cubes:
				UpdateCubeMeshes();
				MPNative.mpUpdateDataTexture(dataTexture.GetNativeTexturePtr());
				break;
		}
	}

	GameObject CreateChildMesh()
	{
		GameObject child = new GameObject("mpMesh");
		child.transform.parent = meshes.transform;
		child.AddComponent<MeshFilter>();
		child.AddComponent<MeshRenderer>();
		return child;
	}

	void UpdateCubeMeshes()
	{
		int numParticles = MPNative.mpGetNumParticles();
		int numActiveChildren = numParticles / 2700 + (numParticles % 2700 == 0 ? 0 : 1);
		while (children.Count < numActiveChildren)
		{
			GameObject child = CreateChildMesh();
			Vector3[] vertices = new Vector3[64800];
			Vector3[] normals = new Vector3[64800];
			Vector2[] uv = new Vector2[64800];
			int[] indices = new int[97200];
			fixed (Vector3* v = vertices) {
			fixed (Vector3* n = normals) {
			fixed (Vector2* t = uv) {
			fixed (int* idx = indices) {
				meshData.vertices = v;
				meshData.normals = n;
				meshData.uv = t;
				meshData.indices = idx;
				MPNative.mpGenerateCubeMesh(children.Count, ref meshData);
			}}}}
			Mesh mesh = child.GetComponent<MeshFilter>().mesh;
			mesh.vertices = vertices;
			mesh.normals = normals;
			mesh.uv = uv;
			mesh.SetIndices(indices, MeshTopology.Triangles, 0);
			children.Add(child);
		}
		UpdateChildMeshes(numActiveChildren);
	}

	void UpdatePointMeshes()
	{
		int numParticles = MPNative.mpGetNumParticles();
		int numActiveChildren = numParticles / 65000 + (numParticles % 65000 == 0 ? 0 : 1);
		while (children.Count < numActiveChildren)
		{
			GameObject child = CreateChildMesh();
			Vector3[] vertices = new Vector3[65000];
			Vector2[] uv = new Vector2[65000];
			int[] indices = new int[65000];
			fixed (Vector3* v = vertices) {
			fixed (Vector2* t = uv) {
			fixed (int* idx = indices) {
				meshData.vertices = v;
				meshData.uv = t;
				meshData.indices = idx;
				MPNative.mpGeneratePointMesh(children.Count, ref meshData);
			}}}
			Mesh mesh = child.GetComponent<MeshFilter>().mesh;
			mesh.vertices = vertices;
			mesh.uv = uv;
			mesh.SetIndices(indices, MeshTopology.Points, 0);
			children.Add(child);
		}
		UpdateChildMeshes(numActiveChildren);
	}

	void UpdateChildMeshes(int numActiveChildren)
	{
		for (int i = 0; i < numActiveChildren; ++i)
		{
			GameObject child = children[i];
			Mesh mesh = child.GetComponent<MeshFilter>().mesh;
			MeshRenderer renderer = child.GetComponent<MeshRenderer>();
			renderer.enabled = true;
			renderer.castShadows = castShadows;
			renderer.receiveShadows = receiveShadows;
			renderer.material = material;
			mesh.bounds = bounds;
		}
		for (int i = numActiveChildren; i < children.Count; ++i)
		{
			children[i].GetComponent<MeshRenderer>().enabled = false;
		}
	}

	void OnRenderObject()
	{
		if (renderMode == RenderMode.Plugin)
		{
			UnityEngine.Camera cam = UnityEngine.Camera.current;
			if (cam)
			{
				MPNative.mpSetViewProjectionMatrix(cam.worldToCameraMatrix, cam.projectionMatrix, cam.transform.position);
			}
			GL.IssuePluginEvent(1);
		}
	}
}