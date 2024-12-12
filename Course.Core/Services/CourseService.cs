﻿using Course.BLL.Extensions;
using Course.BLL.Interfaces;
using Course.DAL;
using Course.DAL.Models;
using Course.Shared;
using Course.Shared.Constants;
using Course.Shared.DTOs;
using Course.Shared.Records;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Course.BLL.Services
{
    public class CourseService : ICourseService
    {
        private readonly IImageService _imageService;
        private readonly AppDbContext _db;

        public CourseService(IImageService imageService, AppDbContext db)
        {
            _imageService = imageService;
            _db = db;
        }

        public async Task<BaseResponse<PostCourseDTO>> CreateCourseAsync(PostCourseDTO course)
        {
            try
            {
                // Add course image to /CourseImages

                var imageResult = await _imageService.AddImageAsync(course.ImageFile, "CourseImages/");
                if (!imageResult.Success)
                {
                    return new BaseResponse<PostCourseDTO>(null, imageResult.Message, [], false);
                }

                // Create new course 
                int maxCourseId = await _db.Courses.MaxAsync(c => c.Id);
                var item = new DAL.Models.Course()
                {
                    Id = maxCourseId + 1,
                    Title = course.Title,
                    Description = course.Description,
                    ImageUrl = imageResult.Data ?? "",
                    HasCertificate = course.HasCertificate,
                    Question = course.Question,
                    Answer = course.Answer,
                    CategoryId = course.CategoryId,
                    AllowDownload = course.AllowDownload,
                    CreatedAt = DateTime.Now,

                };

                int maxGoalId = await _db.Courses.MaxAsync(c => c.Id);
                item.Goals.ConvertGoalsFromStringToList(course.Goals, maxGoalId);

                // Save changes
                await _db.AddAsync(item);
                await _db.SaveChangesAsync();
                return new BaseResponse<PostCourseDTO>(course, Messages.AddedSuccessfully);

            }
            catch (Exception ex)
            {
                return new BaseResponse<PostCourseDTO>(null, Messages.Error, new List<string> { ex.Message }, false);

            }
        }

        public async Task<BaseResponse<IEnumerable<GetAllCoursesDTO>>> GetAllCoursesAsync(Pagination pagination)
        {
            try
            {
                // Get course By id
                var itemsQuery = _db.Courses
                    .Include(c => c.Goals)
                    .Include(c => c.Category);

                // Search
                if (!string.IsNullOrWhiteSpace(pagination.Search))
                    itemsQuery.Where(c => c.Title.Contains(pagination.Search) || c.Description.Contains(pagination.Search));

                // Map
                var items = await itemsQuery.Select(c => new GetAllCoursesDTO
                {
                    Id = c.Id,
                    Title = c.Title,
                    ImageUrl = c.ImageUrl,
                    AllowDownload = c.AllowDownload,
                    CreatedAt = c.CreatedAt,
                    CategoryName = c.Category.Name
                })
                .Take(pagination.PageSize)
                .Skip((pagination.PageIndex - 1) * pagination.PageSize)
                .ToListAsync();

                return new BaseResponse<IEnumerable<GetAllCoursesDTO>>(items, Messages.RetrievedSuccessfully, [], true);

            }
            catch (Exception ex)
            {
                return new BaseResponse<IEnumerable<GetAllCoursesDTO>>(null, Messages.Error, new List<string> { ex.Message }, false);

            }

        }

        public async Task<BaseResponse<GetOneCourseDTO>> GetCourseByIdAsync(int id)
        {
            try
            {
                // Get course By id
                var item = await _db.Courses
                    .Include(c => c.Goals)
                    .Include(c=>c.Category)
                    .Select(c=> new GetOneCourseDTO
                    {
                        Id = c.Id,
                        Title = c.Title,
                        Description = c.Description,
                        ImageUrl = c.ImageUrl,
                        HasCertificate = c.HasCertificate,
                        Question = c.Question,
                        Answer = c.Answer,
                        CategoryId = c.CategoryId,
                        AllowDownload = c.AllowDownload,
                        CreatedAt = c.CreatedAt,
                        CategoryName = c.Category.Name,

                        Goals = c.Goals.Select(g=> new GoalDTO{
                            Id = g.Id,
                            Name = g.Name
                        }).ToList(),

                        Units = c.Units.Select(u=> new UnitDTO
                        {
                            Id = u.Id,
                            Name = u.Name
                        }).ToList()
                        
                    }).FirstOrDefaultAsync(c=>c.Id == id);

                if(item == null)
                    return new BaseResponse<GetOneCourseDTO>(null, Messages.NotFound, [], false);
                 
                return new BaseResponse<GetOneCourseDTO>(item, Messages.RetrievedSuccessfully, [], true);
            }
            catch (Exception ex)
            {
                return new BaseResponse<GetOneCourseDTO>(null, Messages.Error, new List<string> { ex.Message }, false);
            }
        }

        public async Task<BaseResponse<GetOneCourseDTO>> RemoveCourseAsync(int courseId)
        {
            try
            {
                // Get course to be removed
                var item = await _db.Courses.FirstOrDefaultAsync(c=>c.Id == courseId);
                if (item == null)
                    return new BaseResponse<GetOneCourseDTO>(null, Messages.NotFound, [], false);

                // Remove
                _db.Courses.Remove(item);
                await _db.SaveChangesAsync();

                //
                await _imageService.RemoveImageAsync(item.ImageUrl);
                return new BaseResponse<GetOneCourseDTO>(null, Messages.RemovedSuccessfully);

            }
            catch (Exception ex)
            {
                return new BaseResponse<GetOneCourseDTO>(null, Messages.ReferenceError, new List<string> { ex.Message }, false);
            }
        }

        public async Task<BaseResponse<PostCourseDTO>> UpdateCourseAsync(PostCourseDTO course)
        {
            try
            {
                
                // To get the old course image url
                var oldCourse = await _db.Courses.Include(c=>c.Goals).FirstOrDefaultAsync(c=> c.Id == course.Id);
                if (oldCourse == null)
                    return new BaseResponse<PostCourseDTO>(null, Messages.NotFound, [], false);

                // Add course image to /CourseImages if user pass new image 
                var imageResult = new BaseResponse<string>(null, Messages.Error, [], false);
                if (course.ImageFile != null)
                {
                    // Add new image 
                    imageResult = await _imageService.AddImageAsync(course.ImageFile, "CourseImages/");
                    if (!imageResult.Success)
                        return new BaseResponse<PostCourseDTO>(null, imageResult.Message, [], false);

                }

                // Update values
                oldCourse.Id = course.Id;
                oldCourse.Title = course.Title;
                oldCourse.Description = course.Description;
                oldCourse.ImageUrl = (imageResult.Success ? imageResult.Data : oldCourse.ImageUrl) ?? string.Empty;
                oldCourse.HasCertificate = course.HasCertificate;
                oldCourse.Question = course.Question;
                oldCourse.Answer = course.Answer;
                oldCourse.CategoryId = course.CategoryId;
                oldCourse.AllowDownload = course.AllowDownload;
                

                // clear the old goals
                oldCourse.Goals.Clear();

                int maxGoalId = await _db.Goals.MaxAsync(c => c.Id);
                oldCourse.Goals.ConvertGoalsFromStringToList(course.Goals, maxGoalId);

                _db.Courses.Update(oldCourse);
                await _db.SaveChangesAsync();

                // Remove the old image
                if (imageResult.Success)
                {
                    // To delete the old course image
                    var deleteImageResult = await _imageService.RemoveImageAsync(oldCourse.ImageUrl);
                }

                return new BaseResponse<PostCourseDTO>(course, Messages.UpdatedSuccessfully);
            }
            catch (Exception ex)
            {
                return new BaseResponse<PostCourseDTO>(null, Messages.Error, new List<string> { ex.Message }, false);

            }
        }

    }
}