namespace WebAPI_Basics.Domain;

public class OrderNotFoundException(int id) : Exception($"Order with id {id} was not found.");

public class OrderConflictException(string message) : Exception(message);
