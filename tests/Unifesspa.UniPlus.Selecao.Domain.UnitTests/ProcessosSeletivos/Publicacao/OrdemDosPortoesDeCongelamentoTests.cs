namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Publicacao;

using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// As três transições que congelam uma versão recusam na mesma ordem: identificador legível,
/// localidade e conformidade estrutural, nesta sequência. A ordem decide qual código o cliente vê
/// quando há mais de uma pendência.
/// </summary>
public sealed class OrdemDosPortoesDeCongelamentoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Bytes = Encoding.UTF8.GetBytes(new JsonObject { ["status"] = "ok" }.ToJsonString());

    public static TheoryData<string> Transicoes => ["Publicar", "Retificar", "FecharRetificacao"];

    [Theory(DisplayName = "Com várias pendências, a primeira recusa segue a ordem dos portões e cada correção revela a seguinte")]
    [MemberData(nameof(Transicoes))]
    public void PrimeiraRecusa_SegueAOrdemDosPortoes(string transicao)
    {
        Cenario cenario = Cenario.Para(transicao);

        Definir(cenario.Processo, nameof(ProcessoSeletivo.IdentificadorLegivel), null);
        Definir(cenario.Processo, nameof(ProcessoSeletivo.Localidade), null);
        Definir(cenario.Processo, nameof(ProcessoSeletivo.ConfiguracaoTaxaInscricao), null);

        cenario.Congelar().Error!.Code.Should().Be("ProcessoSeletivo.IdentificadorLegivelAusente");

        Definir(cenario.Processo, nameof(ProcessoSeletivo.IdentificadorLegivel), IdentificadoresDeTeste.Novo());
        cenario.Congelar().Error!.Code.Should().Be("ProcessoSeletivo.LocalidadeAusente");

        Definir(cenario.Processo, nameof(ProcessoSeletivo.Localidade), LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!);
        cenario.Congelar().Error!.Code.Should().Be("ProcessoSeletivo.ConformidadeInsuficiente");
    }

    private static void Definir(ProcessoSeletivo processo, string propriedade, object? valor) =>
        typeof(ProcessoSeletivo)
            .GetProperty(propriedade, BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(processo, valor);

    private sealed record Cenario(ProcessoSeletivo Processo, Func<Result<VersaoConfiguracao>> Congelar)
    {
        internal static Cenario Para(string transicao)
        {
            DadosEdital dados = DadosEdital.Criar(
                "001/2026",
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
                new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)),
                Guid.CreateVersion7()).Value!;
            ContextoDeContagemDePrazos contexto = ContextoDeContagemDePrazos.SemCalendario;

            if (transicao == "Publicar")
            {
                ProcessoSeletivo rascunho = ProcessoConformeFactory.Criar();
                return new(rascunho, () => rascunho.Publicar(
                    dados, Bytes, "1.1", "canonical-json/sha256@v1", new string('a', 64), "user-sub-123", new RelogioFixo(Agora),
                    contexto, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum));
            }

            if (transicao == "Retificar")
            {
                ProcessoSeletivo publicado = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out VersaoConfiguracao versao);
                return new(publicado, () => publicado.Retificar(
                    dados, versao, Bytes, "1.1", "canonical-json/sha256@v1", new string('a', 64), "user-sub-123", "Correção do prazo",
                    new RelogioFixo(Agora), contexto, FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum));
            }

            ProcessoSeletivo emSessao = ProcessoEmEdicaoDeTeste.PublicadoSemSessao(out VersaoConfiguracao versaoBase);
            emSessao.AbrirRetificacao(
                "Correção do prazo", versaoBase, identificadorDaVersaoBase: null,
                VersoesPublicadasDeTeste.SoAVigente(emSessao), "user-sub-1", Agora).IsSuccess.Should().BeTrue();
            return new(emSessao, () => emSessao.FecharRetificacao(
                dados, versaoBase, Bytes, "1.1", "canonical-json/sha256@v1", new string('a', 64), "user-sub-123",
                PrecondicaoIfMatch.DeTags([emSessao.ETagDaSessaoEditorial!]), new RelogioFixo(Agora), contexto,
                FatosDeModalidadeDeTeste.DoCatalogo, TiposDeAtoUnicosDeTeste.Nenhum));
        }
    }
}
