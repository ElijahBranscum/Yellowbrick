using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.Internal.Endpoints.StandardLibrary;
using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Sabio.Data;
using Sabio.Data.Providers;
using Sabio.Models;
using Sabio.Models.AppSettings;
using Sabio.Models.Domain.Files;
using Sabio.Models.Domain.Lookups;
using Sabio.Models.Requests.Files;
using Sabio.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Sabio.Services
{
    public class FileService : IFileService
    {
        IDataProvider _dataProvider = null;
        private AWSStorageConfig _AWSConfig;
        
        public FileService(IDataProvider dataProvider, IOptions<AWSStorageConfig> AWSConfig)
        {
            _dataProvider = dataProvider;
            _AWSConfig = AWSConfig.Value;
        }

        public Paged<FileModel> SelectActive(int pageIndex, int pageSize)
        {
            Paged<FileModel> pagedList = null;
            List<FileModel> fileList = null;
            int totalCount = 0;

            _dataProvider.ExecuteCmd("[dbo].[Files_SelectAll_NotDeleted]", inputParamMapper: delegate (SqlParameterCollection param)
            {
                param.AddWithValue("@PageIndex", pageIndex);
                param.AddWithValue("@PageSize", pageSize);
            },
            singleRecordMapper: delegate (IDataReader reader, short set)
            {
                int idx = 0;
                FileModel file = MapSingleFile(reader, ref idx);
                totalCount = reader.GetSafeInt32(idx);

                if (fileList == null)
                {
                    fileList = new List<FileModel>();
                }
                fileList.Add(file);
            });
            if (fileList != null)
            {
                pagedList = new Paged<FileModel>(fileList, pageIndex, pageSize, totalCount);
            }
            return pagedList;
        }

        public Paged<FileModel> SelectAll(int pageIndex, int pageSize)
        {
            Paged<FileModel> pagedList = null;
            List<FileModel> fileList = null;
            int totalCount = 0;

            _dataProvider.ExecuteCmd("[dbo].[Files_SelectAll]", inputParamMapper: delegate (SqlParameterCollection param)
            {
                param.AddWithValue("@PageIndex", pageIndex);
                param.AddWithValue("@PageSize", pageSize);
            },
            singleRecordMapper: delegate (IDataReader reader, short set)
            {
                int idx = 0;
                FileModel file = MapSingleFile(reader, ref idx);
                totalCount = reader.GetSafeInt32(idx);

                if (fileList == null)
                {
                    fileList = new List<FileModel>();
                }
                fileList.Add(file);
            });
            if (fileList != null)
            {
                pagedList = new Paged<FileModel>(fileList, pageIndex, pageSize, totalCount);
            }
            return pagedList;
        }

        public Paged<FileModel> SelectByCreatedBy(int pageIndex, int pageSize, int createdBy)
        {
            Paged<FileModel> pagedList = null;
            List<FileModel> fileList = null;
            int totalCount = 0;

            _dataProvider.ExecuteCmd("[dbo].[Files_Select_ByCreatedBy]", inputParamMapper: delegate (SqlParameterCollection param)
            {
                param.AddWithValue("@CreatedBy", createdBy);
                param.AddWithValue("@PageIndex", pageIndex);
                param.AddWithValue("@PageSize", pageSize);
            },
            singleRecordMapper: delegate (IDataReader reader, short set)
            {
                int idx = 0;
                FileModel file = MapSingleFile(reader, ref idx);
                totalCount = reader.GetSafeInt32(idx);

                if (fileList == null)
                {
                    fileList = new List<FileModel>();
                }
                fileList.Add(file);
            });
            if (fileList != null)
            {
                pagedList = new Paged<FileModel>(fileList, pageIndex, pageSize, totalCount);
            }
            return pagedList;
        }

        public int Add(FileAddRequest model, int userId)
        {
            int id = 0;

            _dataProvider.ExecuteNonQuery("[dbo].[Files_Insert]", inputParamMapper: delegate (SqlParameterCollection param)
            {
                AddCommonParams(model, param, userId);

                SqlParameter idOut = new SqlParameter("@Id", SqlDbType.Int);
                idOut.Direction = ParameterDirection.Output;

                param.Add(idOut);
            },
            returnParameters: delegate (SqlParameterCollection returnParam)
            {
                object oId = returnParam["@Id"].Value;
                int.TryParse(oId.ToString(), out id);
            });
            return id;
        }

        public void UpdateIsDeleted(int id)
        {
            _dataProvider.ExecuteNonQuery("[dbo].[Files_Delete_ById]", inputParamMapper: delegate (SqlParameterCollection param)
            {
                param.AddWithValue("@Id", id);
            },
            returnParameters: null);
        }

        public async Task<List<FileData>> UploadFileAsync(List<IFormFile> files, int userId)
        {

            List<FileData> filesDataList = null;

            RegionEndpoint bucketRegion = RegionEndpoint.GetBySystemName(_AWSConfig.BucketRegion);

            foreach (IFormFile file in files)
            {
                string keyName = Guid.NewGuid().ToString() + "-" + file.FileName;

                var credentials = new BasicAWSCredentials(_AWSConfig.AccessKey, _AWSConfig.Secret);
                var s3Client = new AmazonS3Client(credentials, bucketRegion);

                var fileTransferUtility = new TransferUtility(s3Client);

                await fileTransferUtility.UploadAsync(file.OpenReadStream(), _AWSConfig.BucketName, keyName);

                Console.WriteLine("Upload completed");

                string fileUrl = $"{_AWSConfig.Domain}{keyName}";
                string contentType = file.ContentType;
                string[] typeParts = contentType.Split("/");
                string fileType = typeParts[1];
                string fileName = file.FileName;

            FileAddRequest uploadedFile = new FileAddRequest();
            uploadedFile.Url = fileUrl;
            uploadedFile.Name = fileName;
            uploadedFile.FileType = fileType;

            FileData fileData = new FileData();
            fileData.Id = Add(uploadedFile, userId); 
            fileData.Name = fileName;
            fileData.Url = fileUrl;

                if (filesDataList == null)
                {
                    filesDataList = new List<FileData>();
                }
                filesDataList.Add(fileData);
            }
            return filesDataList;
        }

        private static void AddCommonParams(FileAddRequest model, SqlParameterCollection param, int userId)
        {
            param.AddWithValue("@Name", model.Name);
            param.AddWithValue("@Url", model.Url);
            param.AddWithValue("@FileType", model.FileType);
            param.AddWithValue("@CreatedBy", userId);
        }

        private static FileModel MapSingleFile(IDataReader reader, ref int idx)
        {
            FileModel file = new FileModel();
            file.FileType = new LookUp();

            file.Id = reader.GetSafeInt32(idx++);
            file.Name = reader.GetSafeString(idx++);
            file.Url = reader.GetSafeString(idx++);
            file.FileType.Id = reader.GetSafeInt32(idx++);
            file.FileType.Name = reader.GetSafeString(idx++);
            file.IsDeleted = reader.GetSafeBool(idx++);
            file.CreatedBy = reader.GetSafeInt32(idx++);
            file.DateCreated = reader.GetSafeDateTime(idx++);

            return file;
        }
    }
}
