# A imagem só empacota o que scripts/subir.sh publicou em src/publicado com o SDK do host (o
# mesmo que builda a API, com TreatWarningsAsErrors e os analisadores da raiz).
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY publicado/ ./
ENTRYPOINT ["dotnet", "Unifesspa.UniPlus.Spikes.CifraNoMinio.dll"]
