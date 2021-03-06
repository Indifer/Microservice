﻿#region Copyright
// Copyright Hitachi Consulting
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//    http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.WindowsAzure.Storage.Auth;

namespace Xigadee
{
    public static partial class AzureExtensionMethods
    {
        [ConfigSettingKey("KeyVault")]
        public const string KeyKeyVaultClientId = "KeyVaultClientId";

        [ConfigSettingKey("KeyVault")]
        public const string KeyKeyVaultClientSecret = "KeyVaultClientSecret";

        [ConfigSettingKey("KeyVault")]
        public const string KeyKeyVaultSecretBaseUri = "KeyVaultSecretBaseUri";

        [ConfigSetting("KeyVault")]
        public static string KeyVaultClientId(this IEnvironmentConfiguration config) => config.PlatformOrConfigCache(KeyKeyVaultClientId);

        [ConfigSetting("KeyVault")]
        public static string KeyVaultClientSecret(this IEnvironmentConfiguration config) => config.PlatformOrConfigCache(KeyKeyVaultClientSecret);

        [ConfigSetting("KeyVault")]
        public static string KeyVaultSecretBaseUri(this IEnvironmentConfiguration config) => config.PlatformOrConfigCache(KeyKeyVaultSecretBaseUri);

        [ConfigSetting("KeyVault")]
        public static ClientCredential KeyVaultClientCredential(this IEnvironmentConfiguration config)
        {
            if (string.IsNullOrEmpty(config.KeyVaultClientId()) || string.IsNullOrEmpty(config.KeyVaultClientSecret()))
                return null;

            return new ClientCredential(config.KeyVaultClientId(), config.KeyVaultClientSecret());
        }

        /// <summary>
        /// This extension allows the Key Vault connection values to be manually set as override parameters.
        /// </summary>
        /// <param name="pipeline">The incoming pipeline.</param>
        /// <param name="keyVaultClientId">The Key Vault client Id.</param>
        /// <param name="keyVaultClientSecret">The Key Vault client secret.</param>
        /// <param name="keyVaultSecretBaseUri">The Key Vault secret base uri.</param>
        /// <returns>The passthrough of the pipeline.</returns>
        public static P ConfigOverrideSetKeyVaultConnection<P>(this P pipeline, string keyVaultClientId, string keyVaultClientSecret, string keyVaultSecretBaseUri)
            where P : IPipeline
        {
            pipeline.ConfigurationOverrideSet(KeyKeyVaultClientId, keyVaultClientId);
            pipeline.ConfigurationOverrideSet(KeyKeyVaultClientSecret, keyVaultClientSecret);
            pipeline.ConfigurationOverrideSet(KeyKeyVaultSecretBaseUri, keyVaultSecretBaseUri);
            return pipeline;
        }
    }
}
