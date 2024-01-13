// This file is part of MoonoMod and is licenced under the GNU GPL v3.0.
// See LICENSE file for full text.
// Copyright © 2024 Michael Ripley

using System;

namespace MoonoMod
{
    internal class TranspilerException : Exception
    {
        public TranspilerException(string message) : base(message) { }
    }
}
