using System.Configuration;

namespace Peco.Swedbank.Client.Helpers
{
	public class AppSettings
	{
		public string Get(string name)
		{
			return ConfigurationManager.AppSettings[name];
		}
	}
}