namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

/// <summary>
/// O endereço de uma resposta como a linha o guarda: um componente por coluna, nos dois sentidos entre o
/// <see cref="EnderecoDeclarado"/> e o registro de persistência.
/// </summary>
internal static class EnderecoGravadoDaInscricao
{
    public static EnderecoDeclarado Declarado(EnderecoDaInscricaoRecord linha) => new(
        linha.Cep, linha.Logradouro, linha.Numero, linha.Complemento, linha.Bairro, linha.Distrito,
        linha.MunicipioCodigoIbge, linha.MunicipioNome, linha.Uf, linha.Latitude, linha.Longitude,
        linha.NivelResolucao, linha.Procedencia, linha.CepConfirmado);

    public static EnderecoDaInscricaoRecord NaLinha(
        EnderecoDeclarado endereco, Guid inscricaoId, Guid? ocorrenciaId, string fatoCodigo, OrigemDaResposta origem)
    {
        EnderecoDaInscricaoRecord linha = new()
        {
            Id = Guid.CreateVersion7(),
            InscricaoId = inscricaoId,
            OcorrenciaId = ocorrenciaId,
            FatoCodigo = fatoCodigo,
            Origem = origem,
        };
        GravarComponentes(linha, endereco);

        return linha;
    }

    /// <summary>Leva o endereço que a linha guarda para a linha que a substitui na mesma chave natural.</summary>
    public static void CopiarConteudo(EnderecoDaInscricaoRecord de, EnderecoDaInscricaoRecord para)
    {
        para.Origem = de.Origem;
        GravarComponentes(para, Declarado(de));
    }

    private static void GravarComponentes(EnderecoDaInscricaoRecord linha, EnderecoDeclarado endereco)
    {
        linha.Cep = endereco.Cep;
        linha.Logradouro = endereco.Logradouro;
        linha.Numero = endereco.Numero;
        linha.Complemento = endereco.Complemento;
        linha.Bairro = endereco.Bairro;
        linha.Distrito = endereco.Distrito;
        linha.MunicipioCodigoIbge = endereco.CidadeCodigoIbge;
        linha.MunicipioNome = endereco.CidadeNome;
        linha.Uf = endereco.CidadeUf;
        linha.Latitude = endereco.Latitude;
        linha.Longitude = endereco.Longitude;
        linha.NivelResolucao = endereco.NivelResolucao;
        linha.Procedencia = endereco.Procedencia;
        linha.CepConfirmado = endereco.CepConfirmado;
    }
}
