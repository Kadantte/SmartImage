using System;
using System.Text;

namespace SmartImage.Model
{
	public readonly struct GenericAccount
	{
		public static readonly GenericAccount Null = new GenericAccount();

		public GenericAccount(string username, string password, string email, string apiKey)
		{
			Username = username;
			Password = password;
			Email    = email;
			ApiKey   = apiKey;
		}

		public string Username { get; }
		public string Password { get; }
		public string Email    { get; }
		public string ApiKey   { get; }

		public bool IsNull => this == Null;

		public bool Equals(GenericAccount other)
		{
			return Username == other.Username && Password == other.Password && Email == other.Email &&
			       ApiKey == other.ApiKey;
		}

		public override bool Equals(object obj)
		{
			return obj is GenericAccount other && Equals(other);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Username, Password, Email, ApiKey);
		}

		public static bool operator ==(GenericAccount left, GenericAccount right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(GenericAccount left, GenericAccount right)
		{
			return !left.Equals(right);
		}

		public override string ToString()
		{
			var sb = new StringBuilder();
			sb.AppendFormat("Username: {0}\n", Username);
			sb.AppendFormat("Password: {0}\n", Password);
			sb.AppendFormat("Email: {0}\n", Email);
			sb.AppendFormat("Api key: {0}\n", ApiKey);
			return sb.ToString();
		}
	}
}