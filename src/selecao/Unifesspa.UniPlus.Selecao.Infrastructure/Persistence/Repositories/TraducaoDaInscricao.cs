namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

/// <summary>
/// A tradução da inscrição nos dois sentidos, num lugar só: das linhas gravadas para o <see cref="Inscricao"/> e do
/// <see cref="Inscricao"/> para as linhas e o registro que o EF Core grava. Cifra e decifra por <see cref="ValorGravadoDaResposta"/>,
/// na fronteira do repositório (ADR-0121); nada disso acontece num <c>ValueConverter</c> síncrono.
/// </summary>
internal sealed class TraducaoDaInscricao
{
    private readonly ValorGravadoDaResposta _valores;

    public TraducaoDaInscricao(ValorGravadoDaResposta valores)
    {
        ArgumentNullException.ThrowIfNull(valores);

        _valores = valores;
    }

    public async Task<Inscricao> InscricaoAsync(InscricaoRecord record, LinhasDaInscricao linhas, CancellationToken cancellationToken)
    {
        RespostasLidasDaInscricao lidas = await LerRespostasAsync(linhas, cancellationToken).ConfigureAwait(false);
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos = lidas.Grupos();
        List<AceiteDeTermo> aceites = AceitesLidos(linhas);
        Cpf cpf = CpfDaInscricao.Lido(record.Id, lidas.Informadas);

        return Inscricao.Reidratar(record.Id, record.ProcessoSeletivoId,
            TitularDaInscricao.Reidratar(cpf, record.ContaId, record.EmissorToken),
            EstadoDaInscricao.Reidratar(record.Situacao, record.NumeroInscricao, CancelamentoDaInscricao.Reidratar(record.CanceladaEm, record.CanceladaPor, record.MotivoDoCancelamento)),
            FormularioDaInscricao.Reidratar(record.VersaoFormularioId, PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(record.OfertaCursoId, record.OfertaCursoSegundaOpcaoId), lidas.Informadas, grupos, aceites),
                lidas.Derivadas, lidas.Definicoes), record.Revisao, new DatasDeGravacaoDaInscricao(record.CreatedAt, record.UpdatedAt));
    }

    /// <summary>
    /// As linhas que a inscrição deve ter. Cifra antes de qualquer mutação do que o ChangeTracker
    /// rastreia: se a cifragem falhar, nada fica pela metade para um <c>SaveChangesAsync</c> gravar.
    /// </summary>
    public async Task<LinhasDaInscricao> LinhasAsync(Inscricao entity, CancellationToken cancellationToken)
    {
        LinhasDaInscricao linhas = LinhasDaInscricao.Vazias;

        await AcrescentarRespostasAsync(linhas, entity, entity.Formulario.Preenchimento.Respostas, null, OrigemDaResposta.Informada, cancellationToken).ConfigureAwait(false);
        await AcrescentarRespostasAsync(linhas, entity, entity.Formulario.Derivados, null, OrigemDaResposta.Calculada, cancellationToken).ConfigureAwait(false);
        await AcrescentarOcorrenciasAsync(linhas, entity, cancellationToken).ConfigureAwait(false);
        linhas.Aceites.AddRange(AceitesGravaveis(entity));

        return linhas;
    }

    /// <summary>
    /// Não toca <c>CreatedAt</c>/<c>UpdatedAt</c>: quem o EF rastreia é <see cref="InscricaoRecord"/>,
    /// que não herda <c>EntityBase</c> — o carimbo é de quem chama.
    /// </summary>
    public static void PreencherRegistro(InscricaoRecord record, Inscricao entity, byte[] cpfIndice)
    {
        record.ProcessoSeletivoId = entity.ProcessoSeletivoId;
        record.CpfIndice = cpfIndice;
        record.ContaId = entity.Titular.ContaId;
        record.EmissorToken = entity.Titular.EmissorToken;
        record.Situacao = entity.Estado.Situacao;
        record.VersaoFormularioId = entity.Formulario.VersaoFormularioId;
        record.OfertaCursoId = entity.Formulario.Preenchimento.Opcoes.Primeira;
        record.OfertaCursoSegundaOpcaoId = entity.Formulario.Preenchimento.Opcoes.Segunda;
        record.NumeroInscricao = entity.Estado.NumeroInscricao;
        record.CanceladaEm = entity.Estado.Cancelamento?.CanceladaEm;
        record.CanceladaPor = entity.Estado.Cancelamento?.CanceladaPor;
        record.MotivoDoCancelamento = entity.Estado.Cancelamento?.MotivoDoCancelamento;
        record.Revisao = entity.Revisao;
    }

    private async Task<RespostasLidasDaInscricao> LerRespostasAsync(LinhasDaInscricao linhas, CancellationToken cancellationToken)
    {
        RespostasLidasDaInscricao lidas = new(linhas.Ocorrencias);

        foreach (IGrouping<(Guid? OcorrenciaId, string FatoCodigo), RespostaDaInscricaoRecord> fato in linhas.Respostas
            .GroupBy(static r => (r.OcorrenciaId, r.FatoCodigo)))
        {
            await LerFatoAsync(lidas, fato, cancellationToken).ConfigureAwait(false);
        }

        foreach (EnderecoDaInscricaoRecord endereco in linhas.Enderecos)
        {
            lidas.AcrescentarEndereco(endereco);
        }

        return lidas;
    }

    private async Task LerFatoAsync(
        RespostasLidasDaInscricao lidas,
        IGrouping<(Guid? OcorrenciaId, string FatoCodigo), RespostaDaInscricaoRecord> fato,
        CancellationToken cancellationToken)
    {
        RespostaDaInscricaoRecord primeira = fato.First();
        bool multivalorado = primeira.Posicao is not null;
        List<JsonElement> valores = [];
        foreach (RespostaDaInscricaoRecord linha in fato.OrderBy(static r => r.Posicao))
        {
            valores.Add(await _valores.LerAsync(linha, cancellationToken).ConfigureAwait(false));
        }

        lidas.AcrescentarFato(
            fato.Key.OcorrenciaId,
            primeira.Origem,
            fato.Key.FatoCodigo,
            multivalorado ? JsonSerializer.SerializeToElement(valores) : valores[0],
            DefinicaoLida(primeira, multivalorado));
    }

    private static DefinicaoDaResposta DefinicaoLida(RespostaDaInscricaoRecord primeira, bool multivalorado)
    {
        bool cifrada = primeira.ValorCifrado is not null;
        bool cifraIndividual = cifrada && DefinicaoDaResposta.EhFatoComCifraIndividual(primeira.FatoCodigo);

        return new DefinicaoDaResposta(primeira.Tipo, multivalorado, cifrada && !cifraIndividual, null, cifraIndividual);
    }

    private static List<AceiteDeTermo> AceitesLidos(LinhasDaInscricao linhas) =>
    [
        .. linhas.Aceites.OrderBy(static a => a.Ordem).Select(static a => new AceiteDeTermo(a.TermoCodigo, a.VersaoId, a.HashVersao)),
    ];

    private static IEnumerable<AceiteDaInscricaoRecord> AceitesGravaveis(Inscricao entity) =>
        entity.Formulario.Preenchimento.Aceites.Select((a, i) => new AceiteDaInscricaoRecord
        {
            Id = Guid.CreateVersion7(),
            InscricaoId = entity.Id,
            Ordem = i,
            TermoCodigo = a.Codigo,
            VersaoId = a.VersaoId,
            HashVersao = a.HashVersao,
        });

    private async Task AcrescentarOcorrenciasAsync(LinhasDaInscricao linhas, Inscricao entity, CancellationToken cancellationToken)
    {
        int ordem = 0;
        foreach ((string grupoCodigo, IReadOnlyList<OcorrenciaDeGrupoInscricao> ocorrencias) in entity.Formulario.Preenchimento.Grupos)
        {
            foreach (OcorrenciaDeGrupoInscricao ocorrencia in ocorrencias)
            {
                await AcrescentarOcorrenciaAsync(linhas, entity, grupoCodigo, ocorrencia, ordem++, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task AcrescentarOcorrenciaAsync(
        LinhasDaInscricao linhas,
        Inscricao entity,
        string grupoCodigo,
        OcorrenciaDeGrupoInscricao ocorrencia,
        int ordem,
        CancellationToken cancellationToken)
    {
        OcorrenciaDeGrupoDaInscricaoRecord registro = new()
        {
            Id = Guid.CreateVersion7(),
            InscricaoId = entity.Id,
            GrupoCodigo = grupoCodigo,
            MembroId = ocorrencia.Id,
            Ordem = ordem,
        };
        linhas.Ocorrencias.Add(registro);

        await AcrescentarRespostasAsync(linhas, entity, ocorrencia.Respostas, registro.Id, OrigemDaResposta.Informada, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task AcrescentarRespostasAsync(
        LinhasDaInscricao linhas,
        Inscricao entity,
        IReadOnlyDictionary<string, JsonElement> respostas,
        Guid? ocorrenciaId,
        OrigemDaResposta origem,
        CancellationToken cancellationToken)
    {
        foreach ((string codigo, JsonElement valor) in respostas)
        {
            DefinicaoDaResposta definicao = DefinicaoExigida(entity, codigo);
            if (definicao.Tipo == TipoDoValorDeResposta.Endereco)
            {
                linhas.Enderecos.Add(EnderecoGravavel(entity, codigo, valor, definicao, ocorrenciaId, origem));
                continue;
            }

            linhas.Respostas.AddRange(
                await ValoresGravaveisAsync(entity, codigo, valor, definicao, ocorrenciaId, origem, cancellationToken).ConfigureAwait(false));
        }
    }

    private static DefinicaoDaResposta DefinicaoExigida(Inscricao entity, string codigo) =>
        entity.Formulario.DefinicoesDasRespostas.TryGetValue(codigo, out DefinicaoDaResposta? definicao)
            ? definicao
            : throw new InvalidOperationException($"A inscrição {entity.Id} tem resposta de '{codigo}' sem definição da resposta.");

    private static EnderecoDaInscricaoRecord EnderecoGravavel(
        Inscricao entity, string codigo, JsonElement valor, DefinicaoDaResposta definicao, Guid? ocorrenciaId, OrigemDaResposta origem)
    {
        if (definicao.Identificador || !EnderecoDeclarado.TentarLer(valor, out EnderecoDeclarado endereco))
        {
            throw new InvalidOperationException($"A resposta de '{codigo}' da inscrição {entity.Id} não é um endereço gravável.");
        }

        return EnderecoGravadoDaInscricao.NaLinha(endereco, entity.Id, ocorrenciaId, codigo, origem);
    }

    private async Task<List<RespostaDaInscricaoRecord>> ValoresGravaveisAsync(
        Inscricao entity,
        string codigo,
        JsonElement valor,
        DefinicaoDaResposta definicao,
        Guid? ocorrenciaId,
        OrigemDaResposta origem,
        CancellationToken cancellationToken)
    {
        JsonElement[] valores = definicao.Multivalorado ? [.. valor.EnumerateArray()] : [valor];
        List<RespostaDaInscricaoRecord> linhas = [];
        for (int posicao = 0; posicao < valores.Length; posicao++)
        {
            RespostaDaInscricaoRecord linha = LinhaDaResposta(entity.Id, codigo, definicao, ocorrenciaId, origem, posicao);
            await GravarValorAsync(linha, valores[posicao], definicao, entity, cancellationToken).ConfigureAwait(false);
            linhas.Add(linha);
        }

        return linhas;
    }

    private static RespostaDaInscricaoRecord LinhaDaResposta(
        Guid inscricaoId, string codigo, DefinicaoDaResposta definicao, Guid? ocorrenciaId, OrigemDaResposta origem, int posicao) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            InscricaoId = inscricaoId,
            OcorrenciaId = ocorrenciaId,
            FatoCodigo = codigo,
            Posicao = definicao.Multivalorado ? posicao : null,
            Origem = origem,
            Tipo = definicao.Tipo,
        };

    private Task GravarValorAsync(
        RespostaDaInscricaoRecord linha, JsonElement valor, DefinicaoDaResposta definicao, Inscricao entity, CancellationToken cancellationToken)
    {
        (string Codigo, string? Rotulo)? municipio = definicao.EhMunicipio ? MunicipioGravavel(entity, linha.FatoCodigo, valor) : null;

        return _valores.GravarAsync(linha, valor, definicao, municipio, cancellationToken);
    }

    /// <summary>
    /// O município na forma que a situação da inscrição grava: só o código no rascunho, e o código com o nome conferido na
    /// inscrição enviada. A forma trocada é defeito de quem chama, e não resposta do candidato.
    /// </summary>
    private static (string Codigo, string? Rotulo) MunicipioGravavel(Inscricao entity, string fatoCodigo, JsonElement valor) =>
        ValorDeMunicipio.TentarLer(valor, out string codigo, out string? rotulo) && (rotulo is null) == entity.EhRascunho
            ? (codigo, rotulo)
            : throw new InvalidOperationException(
                $"A resposta de '{fatoCodigo}' da inscrição {entity.Id} não tem a forma de município que a situação {entity.Estado.Situacao} grava.");
}
