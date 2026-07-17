namespace Antigravity.BIMLink.Core.Common
{
    public class Result<T>
    {
        public bool IsSuccess { get; }
        public T Data { get; }
        public string ErrorCode { get; }
        public string ErrorMessage { get; }

        protected Result(bool isSuccess, T data, string errorCode, string errorMsg)
        {
            IsSuccess = isSuccess;
            Data = data;
            ErrorCode = errorCode;
            ErrorMessage = errorMsg;
        }
        
        public static Result<T> Success(T data) => new Result<T>(true, data, null, null);
        public static Result<T> Failure(string code, string message) => new Result<T>(false, default(T), code, message);
    }
}
