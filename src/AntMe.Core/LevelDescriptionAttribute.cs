﻿using System;
using System.Resources;

namespace AntMe
{
    /// <summary>
    /// Level Description Attribute to hold all relevant Information about a Level.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    [Serializable]
    public sealed class LevelDescriptionAttribute : Attribute
    {
        /// <summary>
        /// Default Constructor.
        /// </summary>
        /// <param name="guid">Guid of this Level</param>
        /// <param name="name">Name of this Level</param>
        /// <param name="description">Short Level Description</param>
        public LevelDescriptionAttribute(string guid, string name, string description)
        {
            Init(guid, name, description);
        }

        /// <summary>
        /// Default Constructor.
        /// </summary>
        /// <param name="guid">Guid of this Level</param>
        /// <param name="resourceType">Type of Resource Class for Name and Description</param>
        /// <param name="nameKey">Resource Key for the Level Name</param>
        /// <param name="descriptionKey">Resource Key for the Level Description</param>
        public LevelDescriptionAttribute(string guid, Type resourceType, string nameKey, string descriptionKey)
        {
            // Ressourcen auflösen und Strings auslesen
            var resourceManager = new ResourceManager(resourceType);
            string name = resourceManager.GetString(nameKey);
            string description = resourceManager.GetString(descriptionKey);

            Init(guid, name, description);
        }

        /// <summary>
        /// Initializes the Attribute Stuff and checks the data.
        /// </summary>
        /// <param name="guid">Guid of this Level</param>
        /// <param name="name">Name of this Level</param>
        /// <param name="description">Short Level Description</param>
        private void Init(string guid, string name, string description)
        {
            // Check for valid ID
            Guid id;
            if (!Guid.TryParse(guid, out id))
                throw new ArgumentException("Invalid Guid Format in Level Description.");

            // Check Name
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name),
                    $"The Level Desciption with the ID {id.ToString()} has no valid Name");

            // Check Description
            if (string.IsNullOrEmpty(description))
                throw new ArgumentNullException(nameof(description),
                    $"The Level Desciption with the ID {id.ToString()} and Name '{name}' has no valid Description");

            Id = id;
            Name = name;
            Description = description;

            MinPlayerCount = 0;
            MaxPlayerCount = Level.MaxSlots;
            Hidden = false;
        }

        /// <summary>
        /// Level ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Name of the Level.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Short Description of the Level.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Maximum Count of Players for the Level.
        /// </summary>
        public int MaxPlayerCount { get; set; }

        /// <summary>
        /// Minimum Count of Players for the Level.
        /// </summary>
        public int MinPlayerCount { get; set; }

        /// <summary>
        /// Is this Level free for play or hidden in an Campaign?
        /// </summary>
        public bool Hidden { get; set; }

        /// <summary>
        /// Validates all Level Description Properties.
        /// </summary>
        public void Validate()
        {
            // ID prüfen
            if (Id == Guid.Empty)
                throw new ArgumentException("Id kann nicht empty sein");

            // Name prüfen
            if (string.IsNullOrEmpty(Name))
                throw new ArgumentException("Name kann nicht leer sein");

            // Description prüfen
            if (string.IsNullOrEmpty(Description))
                throw new ArgumentException("Description kann nicht leer sein");

            // Min Player
            if (MinPlayerCount < 0 || MinPlayerCount > Level.MaxSlots)
                throw new ArgumentOutOfRangeException(string.Format("MinPlayerCount muss zwischen 0 und {0} liegen.", Level.MaxSlots));

            // Max Player
            if (MaxPlayerCount < 0 || MaxPlayerCount > Level.MaxSlots)
                throw new ArgumentOutOfRangeException(string.Format("MaxPlayerCount muss zwischen 0 und {0} liegen.", Level.MaxSlots));
        }
    }
}