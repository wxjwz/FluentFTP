﻿using System;
using System.Linq;
using FluentFTP.Helpers;
using FluentFTP.Client.Modules;
using System.Threading;
using System.Threading.Tasks;

namespace FluentFTP {
	public partial class FtpClient {

		/// <summary>
		/// Deletes the specified directory and all its contents.
		/// </summary>
		/// <param name="path">The full or relative path of the directory to delete</param>
		public void DeleteDirectory(string path) {
			// verify args
			if (path.IsBlank()) {
				throw new ArgumentException("Required parameter is null or blank.", "path");
			}

			path = path.GetFtpPath();

			LogFunc(nameof(DeleteDirectory), new object[] { path });
			DeleteDirInternal(path, true, FtpListOption.Recursive);
		}

		/// <summary>
		/// Deletes the specified directory and all its contents.
		/// </summary>
		/// <param name="path">The full or relative path of the directory to delete</param>
		/// <param name="options">Useful to delete hidden files or dot-files.</param>
		public void DeleteDirectory(string path, FtpListOption options) {
			// verify args
			if (path.IsBlank()) {
				throw new ArgumentException("Required parameter is null or blank.", "path");
			}

			path = path.GetFtpPath();

			LogFunc(nameof(DeleteDirectory), new object[] { path, options });
			DeleteDirInternal(path, true, options);
		}

		/// <summary>
		/// Deletes the specified directory and all its contents.
		/// </summary>
		/// <param name="path">The full or relative path of the directory to delete</param>
		/// <param name="deleteContents">If the directory is not empty, remove its contents</param>
		/// <param name="options">Useful to delete hidden files or dot-files.</param>
		protected void DeleteDirInternal(string path, bool deleteContents, FtpListOption options) {
			FtpReply reply;

			path = path.GetFtpPath();

			lock (m_lock) {

				// server-specific directory deletion
				if (!path.IsFtpRootDirectory()) {

					// ask the server handler to delete a directory
					if (ServerHandler != null) {
						if (ServerHandler.DeleteDirectory(this, path, path, deleteContents, options)) {
							return;
						}
					}
				}


				// DELETE CONTENTS OF THE DIRECTORY
				if (deleteContents) {
					// when GetListing is called with recursive option, then it does not
					// make any sense to call another DeleteDirectory with force flag set.
					// however this requires always delete files first.
					var recurse = !WasGetListingRecursive(options);

					// items that are deeper in directory tree are listed first, 
					// then files will be listed before directories. This matters
					// only if GetListing was called with recursive option.
					FtpListItem[] itemList;
					if (recurse) {
						itemList = GetListing(path, options);
					}
					else {
						itemList = GetListing(path, options).OrderByDescending(x => x.FullName.Count(c => c.Equals('/'))).ThenBy(x => x.Type).ToArray();
					}

					// delete the item based on the type
					foreach (var item in itemList) {
						switch (item.Type) {
							case FtpObjectType.File:
								DeleteFile(item.FullName);
								break;

							case FtpObjectType.Directory:
								DeleteDirInternal(item.FullName, recurse, options);
								break;

							default:
								throw new FtpException("Don't know how to delete object type: " + item.Type);
						}
					}
				}


				// SKIP DELETING ROOT DIRS

				// can't delete the working directory and
				// can't delete the server root.
				if (path.IsFtpRootDirectory()) {
					return;
				}


				// DELETE ACTUAL DIRECTORY

				if (!(reply = Execute("RMD " + path)).Success) {
					throw new FtpCommandException(reply);
				}

			}

		}


	}
}