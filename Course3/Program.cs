using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

int idCounter = 1;
Dictionary<int, User> users = new Dictionary<int, User>();
users.Add(idCounter++, new User { Name = "user-1", Email = "user-1@gmail.com", Age = 1 });
users.Add(idCounter++, new User { Name = "user-2", Email = "user-2@gmail.com", Age = 2 });
string apiKey = "123";

app.Use(async (context, next) =>
{
    try
    {
        await next.Invoke();
    }
    catch (Exception e)
    {
        var errorMessage = app.Environment.IsProduction() ? "Internal server error." : e.Message;
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync($"{{error: \"{errorMessage}\"}}");
    }
});

app.Use(async (context, next) =>
{
    string? key = context.Request.Query["key"];
    if (key == apiKey)
    {
        await next.Invoke();
    }
    else
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Unauthorized");
    }
});

app.Use(async (context, next) =>
{
    Console.WriteLine($"Route: {context.Request.Path}");
    Console.WriteLine($"Method: {context.Request.Method}");
    await next.Invoke();
    Console.WriteLine($"Status Code: {context.Response.StatusCode}");
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/users", IResult () =>
{
    List<User> usersList = new List<User>();
    foreach (var user in users.Values)
    {
        usersList.Add(user);
    }

    return TypedResults.Ok(usersList);
});
app.MapGet(
        "/users/{id:int:min(0)}",
        Results<NotFound, Ok<User>> (int id) =>
        {
            User? user = null;
            users.TryGetValue(id, out user);
            if (user == null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(user);
        }
    )
    .Produces<User>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

app.MapPost(
        "/users",
        Results<UnprocessableEntity<string>, Created<User>> (User user) =>
        {
            user.Name = user.Name.Trim();
            user.Email = user.Email.Trim();
            if (String.IsNullOrEmpty(user.Name) || String.IsNullOrEmpty(user.Email))
            {
                return TypedResults.UnprocessableEntity("Name and email are required");
            }

            if (!user.HasValidEmail())
            {
                return TypedResults.UnprocessableEntity("email is invalid");
            }

            users.Add(idCounter++, user);
            return TypedResults.Created($"/users/{idCounter}", user);
        }
    )
    .Produces(StatusCodes.Status201Created)
    .Produces(StatusCodes.Status422UnprocessableEntity);

app.MapPut(
        "/users/{id:int:min(0)}",
        Results<NotFound, UnprocessableEntity<string>, Ok<User>> (int id, User user) =>
        {
            User? previousUser = null;
            users.TryGetValue(id, out previousUser);
            if (previousUser == null)
            {
                return TypedResults.NotFound();
            }

            user.Name = user.Name.Trim();
            user.Email = user.Email.Trim();
            if (String.IsNullOrEmpty(user.Name) || String.IsNullOrEmpty(user.Email))
            {
                return TypedResults.UnprocessableEntity("Name and email are required");
            }

            if (!user.HasValidEmail())
            {
                return TypedResults.UnprocessableEntity("email is invalid");
            }

            users[idCounter] = user;
            return TypedResults.Ok(user);
        }
    )
    .Produces<User>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status422UnprocessableEntity);

app.MapDelete(
        "/users/{id:int:min(0)}",
        Results<NotFound, NoContent> (int id) =>
        {
            if (!users.ContainsKey(id))
            {
                return TypedResults.NotFound();
            }

            users.Remove(id);
            return TypedResults.NoContent();
        }
    )
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound);

app.Run();

public class User
{
    public required string Name { get; set; }
    public required string Email { get; set; }
    public int Age { get; set; }

    public bool HasValidEmail()
    {
        return new EmailAddressAttribute().IsValid(this.Email);
    }
}