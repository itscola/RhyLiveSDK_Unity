using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UniGLTF;
using Unity.Collections;
using UnityEngine;

namespace UniVRM10
{
    /// <summary>
    /// 座標系を変換した Model により、Mesh, Node, BindMatrices を更新する。
    /// buffer, bufferAccessor の更新もある。
    /// </summary>
    class MeshUpdater
    {
        GltfData _data;
        ArrayByteBuffer _buffer;
        List<glTFBufferView> _bufferViews = new List<glTFBufferView>();
        List<glTFAccessor> _accessors = new List<glTFAccessor>();

        public MeshUpdater(GltfData data)
        {
            _data = data;
            _buffer = new ArrayByteBuffer(new byte[data.Bin.Length]);
        }

        int AddBuffer(NativeArray<byte> bytes)
        {
            var bufferView = _buffer.Extend(bytes);
            var index = _bufferViews.Count;
            _bufferViews.Add(bufferView);
            return index;
        }

        int AddAccessor<T>(NativeArray<T> span) where T : struct
        {
            var bufferViewIndex = AddBuffer(span.Reinterpret<byte>(Marshal.SizeOf<T>()));
            var accessor = new glTFAccessor
            {
                bufferView = bufferViewIndex,
                count = span.Length,
                byteOffset = 0,
                componentType = glTFExtensions.GetComponentType<T>(),
                type = glTFExtensions.GetAccessorType<T>(),
            };
            var index = _accessors.Count;
            _accessors.Add(accessor);
            return index;
        }

        int? AddAccessor<T>(BufferAccessor buffer) where T : struct
        {
            if (buffer == null)
            {
                return default;
            }
            return AddAccessor(buffer.GetSpan<T>());
        }

        struct MorphAccessor
        {
            public int? Position;
            public int? Normal;
        };

        public (glTF, ArraySegment<byte>) Update(VrmLib.Model model)
        {
            var gltf = _data.GLTF;

            // copy images
            foreach (var image in gltf.images)
            {
                var bytes = _data.GetBytesFromBufferView(image.bufferView);
                image.bufferView = AddBuffer(bytes);
            }

            // update Mesh
            foreach (var (gltfMesh, mesh) in Enumerable.Zip(gltf.meshes, model.MeshGroups, (l, r) => (l, r.Meshes[0])))
            {
                NativeArray<uint> indices;
                switch (mesh.IndexBuffer.Stride)
                {
                    case 1:
                        {
                            // byte
                            var byte_indices = mesh.IndexBuffer.GetSpan<byte>();
                            indices = _data.NativeArrayManager.Convert(byte_indices, (byte x) => (uint)x);
                            break;
                        }

                    case 2:
                        {
                            // ushort
                            var ushort_indices = mesh.IndexBuffer.GetSpan<ushort>();
                            indices = _data.NativeArrayManager.Convert(ushort_indices, (ushort x) => (uint)x);
                            break;
                        }

                    case 4:
                        {
                            // uint
                            indices = mesh.IndexBuffer.GetSpan<uint>();
                            break;
                        }

                    default:
                        throw new NotImplementedException();
                }
                var position = AddAccessor<Vector3>(mesh.VertexBuffer.Positions);
                var normal = AddAccessor<Vector3>(mesh.VertexBuffer.Normals);
                var uv = AddAccessor<Vector2>(mesh.VertexBuffer.TexCoords);
                var weights = AddAccessor<Vector4>(mesh.VertexBuffer.Weights);
                var joints = AddAccessor<UShort4>(mesh.VertexBuffer.Joints);
                var color = AddAccessor<Vector4>(mesh.VertexBuffer.Colors);

                var morphTargets = new MorphAccessor[] { };
                if (mesh.MorphTargets != null)
                {
                    morphTargets = mesh.MorphTargets.Select(x => new MorphAccessor
                    {
                        Position = AddAccessor<Vector3>(x.VertexBuffer.Positions),
                        Normal = AddAccessor<Vector3>(x.VertexBuffer.Normals),
                    }).ToArray();
                }

                foreach (var (gltfPrim, submesh) in Enumerable.Zip(gltfMesh.primitives, mesh.Submeshes, (l, r) => (l, r)))
                {
                    var subIndices = indices.GetSubArray(submesh.Offset, submesh.DrawCount);
                    gltfPrim.indices = AddAccessor(subIndices);
                    gltfPrim.attributes.POSITION = position.Value;
                    gltfPrim.attributes.NORMAL = normal.GetValueOrDefault(-1); // たぶん、ありえる
                    gltfPrim.attributes.TANGENT = -1;
                    gltfPrim.attributes.COLOR_0 = color.GetValueOrDefault(-1);
                    gltfPrim.attributes.TEXCOORD_0 = uv.GetValueOrDefault(-1); // ありえる？
                    gltfPrim.attributes.TEXCOORD_1 = -1;
                    gltfPrim.attributes.WEIGHTS_0 = weights.GetValueOrDefault(-1);
                    gltfPrim.attributes.JOINTS_0 = joints.GetValueOrDefault(-1);
                    foreach (var (gltfMorph, morph) in Enumerable.Zip(gltfPrim.targets, morphTargets, (l, r) => (l, r)))
                    {
                        gltfMorph.POSITION = morph.Position.GetValueOrDefault(-1);
                        gltfMorph.NORMAL = morph.Normal.GetValueOrDefault(-1);
                        gltfMorph.TANGENT = -1;
                    }
                }
            }

            // update nodes and remove unused skin
            var skins = gltf.skins.ToArray();
            gltf.skins.Clear();
            foreach (var (gltfNode, node) in Enumerable.Zip(gltf.nodes, model.Nodes, (l, r) => (l, r)))
            {
                gltfNode.translation = node.LocalTranslation.ToFloat3();
                gltfNode.rotation = node.LocalRotation.ToFloat4();
                gltfNode.scale = node.LocalScaling.ToFloat3();

                if (gltfNode.mesh >= 0)
                {
                    if (gltfNode.skin >= 0)
                    {
                        //
                        // mesh with skin
                        // only this case, skin is enable
                        // [SkinnedMeshRenderer]
                        //
                        var gltfSkin = skins[gltfNode.skin];
                        // get or create
                        var skinIndex = gltf.skins.IndexOf(gltfSkin);
                        if (skinIndex == -1)
                        {
                            skinIndex = gltf.skins.Count;
                            gltfSkin.inverseBindMatrices = AddAccessor(node.MeshGroup.Skin.InverseMatrices.GetSpan<Matrix4x4>());
                            gltf.skins.Add(gltfSkin);
                        }
                        else
                        {
                            // multi nodes sharing a same skin may be error ?
                            // edge case.
                        }
                        // update
                        gltfNode.skin = skinIndex;
                    }
                    else
                    {
                        //
                        // mesh without skin
                        // [MeshRenderer]
                        //
                    }
                }
                else
                {
                    if (gltfNode.skin >= 0)
                    {
                        //
                        // no mesh but skin
                        // fix error
                        //
                        gltfNode.skin = -1;
                    }
                    else
                    {
                        //
                        // no mesh no skin
                        //
                    }
                }
            }

            // replace
            gltf.bufferViews = _bufferViews;
            gltf.accessors = _accessors;

            return (gltf, _buffer.Bytes);
        }
    }
}
