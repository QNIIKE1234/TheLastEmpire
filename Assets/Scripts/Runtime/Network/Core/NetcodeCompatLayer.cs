#if !UNITY_NETCODE_GAMEOBJECTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Unity.Netcode
{
    public enum NetworkVariableReadPermission
    {
        Everyone = 0,
        Owner = 1
    }

    public enum NetworkVariableWritePermission
    {
        Server = 0,
        Owner = 1
    }

    public interface IReaderWriter
    {
        bool IsReader { get; }
        bool IsWriter { get; }
        BinaryReader Reader { get; }
        BinaryWriter Writer { get; }
    }

    public struct BufferSerializer<T> where T : IReaderWriter
    {
        private T _impl;
        public BufferSerializer(T impl) { _impl = impl; }
        public bool IsReader => _impl.IsReader;
        public bool IsWriter => _impl.IsWriter;

        public void SerializeValue(ref int value)
        {
            if (_impl.IsWriter) _impl.Writer.Write(value);
            else value = _impl.Reader.ReadInt32();
        }

        public void SerializeValue(ref uint value)
        {
            if (_impl.IsWriter) _impl.Writer.Write(value);
            else value = _impl.Reader.ReadUInt32();
        }

        public void SerializeValue(ref ulong value)
        {
            if (_impl.IsWriter) _impl.Writer.Write(value);
            else value = _impl.Reader.ReadUInt64();
        }

        public void SerializeValue(ref byte value)
        {
            if (_impl.IsWriter) _impl.Writer.Write(value);
            else value = _impl.Reader.ReadByte();
        }

        public void SerializeValue(ref short value)
        {
            if (_impl.IsWriter) _impl.Writer.Write(value);
            else value = _impl.Reader.ReadInt16();
        }

        public void SerializeValue(ref ushort value)
        {
            if (_impl.IsWriter) _impl.Writer.Write(value);
            else value = _impl.Reader.ReadUInt16();
        }

        public void SerializeValue(ref float value)
        {
            if (_impl.IsWriter) _impl.Writer.Write(value);
            else value = _impl.Reader.ReadSingle();
        }

        public void SerializeValue(ref bool value)
        {
            if (_impl.IsWriter) _impl.Writer.Write(value);
            else value = _impl.Reader.ReadBoolean();
        }

        public void SerializeValue(ref string value)
        {
            if (_impl.IsWriter) _impl.Writer.Write(value ?? string.Empty);
            else value = _impl.Reader.ReadString();
        }

        public void SerializeValue(ref Vector3 value)
        {
            if (_impl.IsWriter)
            {
                _impl.Writer.Write(value.x);
                _impl.Writer.Write(value.y);
                _impl.Writer.Write(value.z);
            }
            else
            {
                value = new Vector3(_impl.Reader.ReadSingle(), _impl.Reader.ReadSingle(), _impl.Reader.ReadSingle());
            }
        }

        public void SerializeValue(ref Vector2 value)
        {
            if (_impl.IsWriter)
            {
                _impl.Writer.Write(value.x);
                _impl.Writer.Write(value.y);
            }
            else
            {
                value = new Vector2(_impl.Reader.ReadSingle(), _impl.Reader.ReadSingle());
            }
        }
    }

    public struct BinaryBufferReaderWriter : IReaderWriter
    {
        public bool IsReader { get; private set; }
        public bool IsWriter => !IsReader;
        public BinaryReader Reader { get; private set; }
        public BinaryWriter Writer { get; private set; }

        public static BinaryBufferReaderWriter CreateWriter(BinaryWriter writer)
        {
            return new BinaryBufferReaderWriter { IsReader = false, Writer = writer };
        }

        public static BinaryBufferReaderWriter CreateReader(BinaryReader reader)
        {
            return new BinaryBufferReaderWriter { IsReader = true, Reader = reader };
        }
    }

    public interface INetworkSerializable
    {
        void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class ServerRpcAttribute : Attribute
    {
        public bool RequireOwnership { get; set; } = true;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class ClientRpcAttribute : Attribute
    {
    }

    public struct RpcReceiveParams
    {
        public ulong SenderClientId;
    }

    public struct RpcSendParams
    {
        public IReadOnlyList<ulong> TargetClientIds;
    }

    public struct ServerRpcParams
    {
        public RpcReceiveParams Receive;
    }

    public struct ClientRpcParams
    {
        public RpcSendParams Send;
    }

    public class NetworkVariable<T>
    {
        public delegate void OnValueChangedDelegate(T previousValue, T newValue);
        public OnValueChangedDelegate OnValueChanged;

        private T _value;
        public NetworkVariableReadPermission ReadPerm { get; }
        public NetworkVariableWritePermission WritePerm { get; }

        public NetworkVariable(
            T value = default,
            NetworkVariableReadPermission readPerm = NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission writePerm = NetworkVariableWritePermission.Server)
        {
            _value = value;
            ReadPerm = readPerm;
            WritePerm = writePerm;
        }

        public T Value
        {
            get => _value;
            set
            {
                if (!EqualityComparer<T>.Default.Equals(_value, value))
                {
                    T prev = _value;
                    _value = value;
                    OnValueChanged?.Invoke(prev, _value);
                }
            }
        }
    }

    public interface INetworkPrefabInstanceHandler
    {
        NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation);
        void Destroy(NetworkObject networkObject);
    }

    public class NetworkPrefabHandler
    {
        private readonly Dictionary<GameObject, INetworkPrefabInstanceHandler> _handlers = new Dictionary<GameObject, INetworkPrefabInstanceHandler>();

        public void AddHandler(GameObject prefab, INetworkPrefabInstanceHandler instanceHandler)
        {
            if (prefab != null && instanceHandler != null)
            {
                _handlers[prefab] = instanceHandler;
            }
        }

        public void RemoveHandler(GameObject prefab)
        {
            if (prefab != null)
            {
                _handlers.Remove(prefab);
            }
        }
    }

    public class NetworkObject : MonoBehaviour
    {
        public ulong NetworkObjectId { get; internal set; }
        public ulong OwnerClientId { get; internal set; }
        public bool IsSpawned { get; internal set; }
        public bool IsOwner => NetworkManager.Singleton == null || OwnerClientId == NetworkManager.Singleton.LocalClientId;
        public bool IsLocalPlayer => IsOwner && CompareTag("Player");
        public bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        public bool IsClient => NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient;
        public bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        public void Spawn(bool destroyWithScene = false)
        {
            IsSpawned = true;
            var behaviours = GetComponentsInChildren<NetworkBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                behaviours[i].InternalNotifyNetworkSpawn();
            }
        }

        public void SpawnAsPlayerObject(ulong clientId, bool destroyWithScene = false)
        {
            OwnerClientId = clientId;
            Spawn(destroyWithScene);
        }

        public void Despawn(bool destroy = true)
        {
            if (!IsSpawned) return;
            IsSpawned = false;
            var behaviours = GetComponentsInChildren<NetworkBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                behaviours[i].InternalNotifyNetworkDespawn();
            }
            if (destroy)
            {
                Destroy(gameObject);
            }
        }
    }

    public abstract class NetworkBehaviour : MonoBehaviour
    {
        private NetworkObject _networkObject;
        public NetworkObject NetworkObject
        {
            get
            {
                if (_networkObject == null)
                {
                    _networkObject = GetComponentInParent<NetworkObject>();
                    if (_networkObject == null)
                    {
                        _networkObject = gameObject.AddComponent<NetworkObject>();
                    }
                }
                return _networkObject;
            }
        }

        public bool IsOwner => NetworkObject.IsOwner;
        public bool IsLocalPlayer => NetworkObject.IsLocalPlayer;
        public bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        public bool IsClient => NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient;
        public bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        public bool IsSpawned => NetworkObject.IsSpawned;
        public ulong OwnerClientId => NetworkObject.OwnerClientId;
        public ulong NetworkObjectId => NetworkObject.NetworkObjectId;

        internal void InternalNotifyNetworkSpawn() => OnNetworkSpawn();
        internal void InternalNotifyNetworkDespawn() => OnNetworkDespawn();

        public virtual void OnNetworkSpawn() { }
        public virtual void OnNetworkDespawn() { }
    }

    public class NetworkManager : MonoBehaviour
    {
        public const ulong ServerClientId = 0;
        public static NetworkManager Singleton { get; private set; }

        public bool IsServer { get; private set; }
        public bool IsClient { get; private set; }
        public bool IsHost => IsServer && IsClient;
        public bool IsListening => IsServer || IsClient;
        public ulong LocalClientId { get; internal set; } = ServerClientId;

        public NetworkPrefabHandler PrefabHandler { get; } = new NetworkPrefabHandler();

        public event Action<ulong> OnClientConnectedCallback;
        public event Action<ulong> OnClientDisconnectCallback;

        private readonly List<ulong> _connectedClientsIds = new List<ulong>();
        public IReadOnlyList<ulong> ConnectedClientsIds => _connectedClientsIds;

        private void Awake()
        {
            if (Singleton == null)
            {
                Singleton = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Singleton != this)
            {
                Destroy(gameObject);
            }
        }

        public bool StartHost()
        {
            IsServer = true;
            IsClient = true;
            LocalClientId = ServerClientId;
            _connectedClientsIds.Clear();
            _connectedClientsIds.Add(ServerClientId);
            OnClientConnectedCallback?.Invoke(ServerClientId);
            return true;
        }

        public bool StartServer()
        {
            IsServer = true;
            IsClient = false;
            LocalClientId = ServerClientId;
            _connectedClientsIds.Clear();
            return true;
        }

        public bool StartClient()
        {
            IsServer = false;
            IsClient = true;
            return true;
        }

        public void Shutdown()
        {
            IsServer = false;
            IsClient = false;
            _connectedClientsIds.Clear();
        }

        internal void RegisterConnectedClient(ulong clientId)
        {
            if (!_connectedClientsIds.Contains(clientId))
            {
                _connectedClientsIds.Add(clientId);
                OnClientConnectedCallback?.Invoke(clientId);
            }
        }

        internal void UnregisterConnectedClient(ulong clientId)
        {
            if (_connectedClientsIds.Remove(clientId))
            {
                OnClientDisconnectCallback?.Invoke(clientId);
            }
        }
    }
}
#endif
