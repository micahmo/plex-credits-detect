﻿using SoundFingerprinting;
using SoundFingerprinting.Data;
using SoundFingerprinting.Extensions.LMDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace plexCreditsDetect.Database
{
    [Obsolete("LMDBFingerprintDatabase database not functional at present. Use InMemoryFingerprintDatabase.", true)]
    internal class LMDBFingerprintDatabase : IFingerprintDatabase
    {
        LMDBModelService modelService = null;
        LMDBConfiguration modelConfig = null;

        Dictionary<string, Episode> detectionNeeded = new Dictionary<string, Episode>();

        public LMDBFingerprintDatabase()
        {
        }
        public LMDBFingerprintDatabase(string path, LMDBConfiguration config = null)
        {
            modelConfig = config;
            LoadDatabase(path);
        }

        public void LoadDatabase(string path)
        {
            if (path == null || path == "")
            {
                throw new ArgumentException("Invalid database path");
            }

            if (File.Exists(path))
            {
                throw new ArgumentException("Invalid database path");
            }
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }


            if (modelConfig == null)
            {
                modelConfig = new LMDBConfiguration();
            }
            if (modelService == null)
            {
                modelService = new LMDBModelService(path, modelConfig);
            }

        }

        public void CloseDatabase()
        {
            if (modelService != null)
            {
                modelService.Dispose();
                modelService = null;
            }
        }

        public AVHashes GetTrackHash(string id, bool isCredits, int partNum = -1)
        {
            return modelService.ReadHashesByTrackId(id + isCredits);
        }
        public Episode GetEpisode(string id)
        {
            long tmp;
            bool tmp2;

            if (modelService == null)
            {
                throw new InvalidOperationException("Database connection not established");
            }

            var track = modelService.ReadTrackById(id); // broken

            if (track == null)
            {
                if (detectionNeeded.ContainsKey(id))
                {
                    return detectionNeeded[id];
                }

                return null;
            }

            Episode ep = new Episode(id);
            ep.id = track.Id;
            ep.name = track.Title;
            ep.dir = track.Artist;

            ep.LastWriteTimeUtcInDB = DateTime.MinValue;
            ep.FileSizeInDB = 0;
            ep.DetectionPending = true;

            if (long.TryParse(track.MetaFields["LastWriteTimeUtc"], out tmp))
            {
                ep.LastWriteTimeUtcInDB = DateTime.FromFileTimeUtc(tmp);
            }

            if (long.TryParse(track.MetaFields["FileSize"], out tmp))
            {
                ep.FileSizeInDB = tmp;
            }

            if (bool.TryParse(track.MetaFields["DetectionPending"], out tmp2))
            {
                ep.DetectionPending = tmp2;
            }

            return ep;
        }

        public List<Episode> GetPendingEpisodes()
        {
            return detectionNeeded.Values.ToList();
        }

        public Episode GetOnePendingEpisode()
        {
            return detectionNeeded.FirstOrDefault().Value;
        }

        public void DeleteEpisode(Episode ep)
        {
            modelService.DeleteTrack(ep.id + true.ToString());
            modelService.DeleteTrack(ep.id + false.ToString());
        }

        public void Insert(Episode ep)
        {
            TrackInfo trackinfo = CreateTrack(ep, true, 0); // broken right now

            if (GetEpisode(trackinfo.Id) == null)
            {
                detectionNeeded[ep.id] = ep;
            }
            else
            {
                if (modelService == null)
                {
                    throw new InvalidOperationException("Database connection not established");
                }

                modelService.UpdateTrack(trackinfo);
            }
        }

        public void InsertHash(Episode ep, AVHashes hashes, MediaType avtype, bool isCredits, double start, int partNum = -1)
        {
            if (modelService == null)
            {
                throw new InvalidOperationException("Database connection not established");
            }

            TrackInfo trackinfo = CreateTrack(ep, isCredits, start);

            if (GetEpisode(trackinfo.Id) != null)
            {
                DeleteEpisode(ep);
            }
            modelService.Insert(trackinfo, hashes);
        }

        public TrackInfo CreateTrack(Episode ep, bool isCredits, double start)
        {
            return new TrackInfo(ep.id + isCredits.ToString(), ep.name, ep.dir, new Dictionary<string, string>()
            {
                { "name", ep.name },
                { "dir", ep.dir },
                { "LastWriteTimeUtc", ep.LastWriteTimeUtcOnDisk.ToFileTimeUtc().ToString() },
                { "FileSize", ep.FileSizeOnDisk.ToString() },
                { "start", start.ToString() },
                { "isCredits", isCredits.ToString() },
                { "DetectionPending", ep.DetectionPending.ToString() }
            });
        }

        public void SetupNewScan()
        {

        }

        public IModelService GetModelService()
        {
            return modelService;
        }

        public List<string> GetPendingDirectories()
        {
            List<string> dirs = new List<string>();

            foreach (var item in detectionNeeded)
            {
                if (item.Value.Exists && !dirs.Contains(item.Value.fullDirPath))
                {
                    dirs.Add(item.Value.fullDirPath);
                }
            }

            return dirs;
        }

        public void InsertTiming(Episode ep, Segment segment, bool isPlexIntro)
        {
            throw new NotImplementedException();
        }

        public void DeleteEpisodeTimings(Episode ep)
        {
            throw new NotImplementedException();
        }
    }
}
