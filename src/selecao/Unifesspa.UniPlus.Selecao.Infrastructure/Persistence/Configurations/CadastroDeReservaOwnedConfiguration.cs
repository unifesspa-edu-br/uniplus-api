namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence.CodigosDeContrato;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Converters;

/// <summary>
/// O cadastro de reserva por chamada, guardado numa linha por chamada declarada. O agregado o expõe como
/// <see cref="CadastroDeReservaPorChamada"/>, que não é mapeado: o que o banco guarda são os itens da lista do processo.
/// </summary>
internal static class CadastroDeReservaOwnedConfiguration
{
    private const string ListaDoProcesso = "_cadastroDeReserva";

    internal static void ConfigurarCadastroDeReserva(this EntityTypeBuilder<ProcessoSeletivo> processo)
    {
        ArgumentNullException.ThrowIfNull(processo);
        processo.Ignore(p => p.CadastroDeReserva);
        processo.OwnsMany<CadastroDeReservaDaChamada>(ListaDoProcesso, ConfigurarChamada);
    }

    private static void ConfigurarChamada(OwnedNavigationBuilder<ProcessoSeletivo, CadastroDeReservaDaChamada> chamada)
    {
        chamada.ClassificaPorPadrao(ClassificacaoDeProtecao.Publico);
        chamada.ToTable("cadastros_de_reserva", ConfigurarTabela);
        chamada.WithOwner().HasForeignKey("ProcessoSeletivoId").HasConstraintName("fk_cadastros_de_reserva_processo_seletivo");
        ConfigurarColunas(chamada);
        chamada.HasIndex("ProcessoSeletivoId", nameof(CadastroDeReservaDaChamada.Chamada)).IsUnique().HasDatabaseName("ux_cadastros_de_reserva_processo_chamada");
    }

    private static void ConfigurarColunas(OwnedNavigationBuilder<ProcessoSeletivo, CadastroDeReservaDaChamada> chamada)
    {
        chamada.Property<int>("Id").ValueGeneratedOnAdd().HasComment("Identificador técnico da linha, substituída com a declaração.");
        chamada.HasKey("Id");
        chamada.Property<Guid>("ProcessoSeletivoId").HasComment("Processo seletivo que declara o cadastro de reserva.");
        chamada.Property(c => c.Chamada).HasConversion<NumeroDaChamadaValueConverter>().HasColumnName("numero_da_chamada").IsRequired()
            .HasComment("Número da chamada do processo, a partir de 1.");
        chamada.Property(c => c.PessoasDaReservaPorVaga).HasColumnName("pessoas_da_reserva_por_vaga").IsRequired()
            .HasComment("Quantas pessoas da reserva a chamada convoca por vaga, além de quem ocupa a vaga; zero é a chamada sem reserva.");
        chamada.Property(c => c.Participacao).HasColumnName("participacao_da_reserva").HasMaxLength(TamanhoDoCodigoDeContrato.Maximo)
            .HasComment("O que a reserva faz quando a chamada a convoca; vazio na chamada sem reserva.");
    }

    private static void ConfigurarTabela(OwnedNavigationTableBuilder<ProcessoSeletivo, CadastroDeReservaDaChamada> tabela)
    {
        tabela.HasComment("Cadastro de reserva por chamada declarado pelo processo seletivo: quantas pessoas da reserva cada chamada convoca por vaga e o que a reserva faz na chamada.");
        tabela.HasCheckConstraint("ck_cadastros_de_reserva_numero_da_chamada", "numero_da_chamada >= 1");
        tabela.HasCheckConstraint("ck_cadastros_de_reserva_pessoas_da_reserva_por_vaga", "pessoas_da_reserva_por_vaga >= 0");
        tabela.HasCheckConstraint(
            "ck_cadastros_de_reserva_participacao_da_reserva",
            $"participacao_da_reserva IS NULL OR participacao_da_reserva IN ({SqlDosCodigosDeContrato.Lista<ParticipacaoDaReserva>()})");
        tabela.HasCheckConstraint(
            "ck_cadastros_de_reserva_participacao_e_quantidade",
            "(pessoas_da_reserva_por_vaga = 0) = (participacao_da_reserva IS NULL)");
    }
}
