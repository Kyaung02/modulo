using Unity.Netcode.Components;
using UnityEngine;

namespace Network
{
    /// <summary>
    /// A helper class to allow Client Authority for NetworkTransform.
    /// This allows the client to move their own object immediately without waiting for the server.
    /// </summary>
    public class ClientNetworkTransform : NetworkTransform
    {
        /// <summary>
        /// Returns false to indicate that the Client has authority, not the Server.
        /// </summary>
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}
