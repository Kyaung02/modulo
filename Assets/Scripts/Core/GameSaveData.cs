using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 전체 저장 데이터
/// </summary>
[Serializable]
public class GameSaveData
{
    public string saveVersion = "1.0";
    public string saveTimestamp;
    public GoalSaveData goalData;
    public ModuleSaveData moduleData;
    public List<ComponentSaveData> components = new List<ComponentSaveData>();
}

/// <summary>
/// 목표 시스템 저장 데이터
/// </summary>
[Serializable]
public class GoalSaveData
{
    public int currentLevelIndex;
    public int currentDeliverCount;
}

/// <summary>
/// 모듈 관리자 저장 데이터
/// </summary>
[Serializable]
public class ModuleSaveData
{
    public int gridWidth;
    public int gridHeight;
    public float cellSize;
    public Vector2 originPosition;
}

/// <summary>
/// 컴포넌트 저장 데이터
/// </summary>
[Serializable]
public class ComponentSaveData
{
    public string componentType; // 컴포넌트 타입 이름 (예: "CombinerComponent", "PortComponent")
    public string prefabPath; // Resources 경로 (예: "NetworkPrefabs/CombinerComponent")
    
    // 위치 및 회전
    public Vector2Int gridPosition;
    public Direction rotation;
    
    // 세션 간 고유 식별을 위한 ID
    public ulong savedNetworkId;
    
    // 부모 모듈 정보 (RecursiveModule 내부 컴포넌트인 경우)
    public ulong parentModuleNetworkId; // 0이면 루트 월드
    
    // 상태
    public string heldWordId; // 보유 중인 단어 ID (비어있으면 null)
    
    // Combiner 전용
    public int isFlipped; // CombinerComponent의 Flip 상태 (0 또는 1)
    
    // Port 전용
    public Direction wallDirection; // PortComponent의 wallDirection
    public string infiniteSourceWordId; // Port의 무한 소스 단어 ID
    
    // RecursiveModule 전용
    public bool isRecursiveModule; // RecursiveModuleComponent인지 여부
    // 추가 RecursiveModule 데이터가 필요하면 여기에 추가
}
