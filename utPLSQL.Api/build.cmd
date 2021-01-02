msbuild utPLSQL.Api.sln /p:Configuration=Release

cd utPLSQL.Api

nuget pack -Prop Configuration=Release