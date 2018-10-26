﻿
namespace AntMe
{
    /// <summary>
    /// Base Class for all Level State Properties.
    /// </summary>
    public abstract class LevelStateProperty : StateProperty
    {
        /// <summary>
        /// Default Constructor for the Deserializer.
        /// </summary>
        protected LevelStateProperty() { }

        /// <summary>
        /// Default Constructor for the Type Mapper.
        /// </summary>
        /// <param name="level">Related Level</param>
        /// <param name="property">Related Level Property</param>
        protected LevelStateProperty(Level level, LevelProperty property) : base(property) { }
    }
}
