﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using JetBrains.Annotations;
using Microsoft.Data.Entity.Migrations.Operations;

// ReSharper disable once CheckNamespace

namespace Microsoft.Data.Entity.Migrations.Operations {
    public class DropDatabaseOperation : MigrationOperation {
        public virtual string Name {
            get;
            [param: NotNull]
            set;
        }
    }
}