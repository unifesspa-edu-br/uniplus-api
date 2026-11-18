namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.Services.Inscricoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static FormularioDoEnvioDeTeste;

internal sealed class FormularioASubmeterDeTeste
{
    private readonly Inscricao _inscricao;
    private readonly InscricaoRequest _pedido;
    private readonly IInscricaoRepository _inscricoes = Substitute.For<IInscricaoRepository>();
    private readonly IProcessoSeletivoRepository _processos = Substitute.For<IProcessoSeletivoRepository>();
    private readonly IRegistroCodecsEnvelope _codecs = Substitute.For<IRegistroCodecsEnvelope>();

    public FormularioASubmeterDeTeste(IReadOnlyList<FatoColetado> itens, IReadOnlyList<GrupoColetado>? grupos, Action<ProcessoSeletivo>? ajustar = null)
    {
        Cpf cpf = PersonasDeTeste.FernandaDrumond;
        (VersaoConfiguracao versao, ConfiguracaoPublicada configuracao, VersaoTermoEscolhida termo) = MontarVersaoComItemObrigatorioETermo(itens, grupos, ajustar);
        _inscricao = SubmeterDeTeste.NovaInscricao(cpf, "conta-1", versao.Id);
        _pedido = RequestComApelidoETermoAceito(cpf, termo, configuracao);
        _inscricoes.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(_inscricao);
        DivulgacaoDeTeste.ConfigurarVersao(_processos, _codecs, versao, configuracao);
    }

    public string? NomeSocialDaConta { get; init; }
    public IConferenciaEnderecoGeo Geo { get; init; } = SubmeterDeTeste.ConferenciaQueAceitaTudo();
    public IReadOnlyCollection<string> Ausentes { get; init; } = [];

    public async Task<(Result<ReciboDaInscricaoDto> Resultado, Inscricao Inscricao)> SubmeterAsync(
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>? respostasDosGrupos = null)
    {
        InscricaoRequest pedido = _pedido with { Respostas = MesclarRespostas(respostas), Grupos = respostasDosGrupos };
        Result<ReciboDaInscricaoDto> resultado = await new SubmissaoDeTeste(_inscricoes, _processos, _codecs, Substitute.For<IUnitOfWork>(),
            SubmeterDeTeste.UsuarioComCpf(PersonasDeTeste.FernandaDrumond, nomeSocial: NomeSocialDaConta))
        { Geo = Geo }
            .SubmeterAsync(new SubmeterInscricaoCommand(ProcessoId, pedido, PrecondicaoIfMatch.DeTags([_inscricao.ETag])));
        SubmeterDeTeste.Guardar(_inscricao, _inscricoes);
        return (resultado, _inscricao);
    }

    private Dictionary<string, JsonElement> MesclarRespostas(IReadOnlyDictionary<string, JsonElement> respostas)
    {
        Dictionary<string, JsonElement> todas = new(_pedido.Respostas!, StringComparer.Ordinal);
        foreach (string fato in Ausentes)
        {
            todas.Remove(fato);
        }
        foreach ((string fato, JsonElement valor) in respostas)
        {
            todas[fato] = valor;
        }
        return todas;
    }
}
