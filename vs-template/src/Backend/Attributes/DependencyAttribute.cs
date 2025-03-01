namespace Server.Attributes
{
    public class DependencyAttribute : System.Attribute
    {
        private string dependency;

        public DependencyAttribute(string dependency)
        {
            this.dependency = dependency;
        }
    }
}
