// ── WireData.cs ──
// 와이어 연결 하나를 표현하는 값 타입.
// WireManager의 _wires Dictionary에 저장되며, 직렬화 대상.

using System;
using UnityEngine;

namespace Arcpunk.Power
{
    [Serializable]
    public struct WireData : IEquatable<WireData>
    {
        public int WireId;
        public Vector3Int BlockA;
        public Vector3Int BlockB;

        // 미래 확장용: 거리 비례 전력 손실 등
        public float Resistance;

        public WireData(int wireId, Vector3Int a, Vector3Int b)
        {
            WireId = wireId;
            BlockA = a;
            BlockB = b;
            Resistance = 0f;
        }

        /// <summary>이 와이어가 특정 좌표에 연결되어 있는지 확인.</summary>
        public bool IsConnectedTo(Vector3Int pos)
        {
            return BlockA == pos || BlockB == pos;
        }

        /// <summary>주어진 한쪽 끝의 반대편 좌표를 반환.</summary>
        public Vector3Int GetOtherEnd(Vector3Int pos)
        {
            if (BlockA == pos) return BlockB;
            if (BlockB == pos) return BlockA;
            throw new ArgumentException($"Position {pos} is not an endpoint of wire {WireId}");
        }

        // ── IEquatable ──
        public bool Equals(WireData other)
        {
            return WireId == other.WireId;
        }

        public override bool Equals(object obj)
        {
            return obj is WireData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return WireId;
        }

        public override string ToString()
        {
            return $"Wire#{WireId} ({BlockA} <-> {BlockB})";
        }
    }

    // ── 직렬화용 래퍼 ──
    // JsonUtility는 배열 루트를 직접 지원하지 않으므로 래퍼 필요.
    [Serializable]
    public class WireSaveData
    {
        public WireSerializable[] Wires;
    }

    // Vector3Int는 JsonUtility에서 직렬화가 불안정할 수 있으므로
    // int 필드로 풀어서 저장.
    [Serializable]
    public struct WireSerializable
    {
        public int WireId;
        public int AX, AY, AZ;
        public int BX, BY, BZ;
        public float Resistance;

        public static WireSerializable FromWireData(WireData w)
        {
            return new WireSerializable
            {
                WireId = w.WireId,
                AX = w.BlockA.x, AY = w.BlockA.y, AZ = w.BlockA.z,
                BX = w.BlockB.x, BY = w.BlockB.y, BZ = w.BlockB.z,
                Resistance = w.Resistance,
            };
        }

        public WireData ToWireData()
        {
            return new WireData
            {
                WireId = WireId,
                BlockA = new Vector3Int(AX, AY, AZ),
                BlockB = new Vector3Int(BX, BY, BZ),
                Resistance = Resistance,
            };
        }
    }
}
